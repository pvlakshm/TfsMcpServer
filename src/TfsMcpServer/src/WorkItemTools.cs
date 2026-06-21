using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace TfsMcpServer.Tools;

/// <summary>
/// MCP tools that expose TFS 2013 Work Item operations.
/// Each method only orchestrates: call <see cref="IWorkItemStore"/>, shape the
/// response via <see cref="WorkItemViewModels"/>, and serialize. Response
/// formatting itself lives in WorkItemViewModels so this class stays thin.
/// </summary>
[McpServerToolType]
public static class WorkItemTools
{
    private static IWorkItemStore Store => ServiceLocator.WorkItemStore;

    // -------------------------------------------------------------------------
    // QUERY
    // -------------------------------------------------------------------------

    [McpServerTool, Description(
        "Run a WIQL (Work Item Query Language) query and return matching work items. " +
        "Mock projects available: 'FabrikamFiber', 'AdventureWorks'. " +
        "Example: SELECT [System.Id],[System.Title],[System.State] " +
        "FROM WorkItems WHERE [System.TeamProject]='FabrikamFiber' ORDER BY [System.Id]")]
    public static string QueryWorkItems(
        [Description("The WIQL SELECT query to execute.")]
        string wiql,
        [Description("Maximum number of results to return (default 50, max 200).")]
        int maxResults = 50)
    {
        maxResults = Math.Clamp(maxResults, 1, 200);
        var items = Store.Query(wiql, maxResults);
        return JsonSerializer.Serialize(WorkItemViewModels.QueryResult(items), JsonOptions.Default);
    }

    // -------------------------------------------------------------------------
    // GET
    // -------------------------------------------------------------------------

    [McpServerTool, Description(
        "Retrieve full details of a single work item by its numeric ID. " +
        "Mock IDs available: 1–10.")]
    public static string GetWorkItem(
        [Description("The numeric ID of the work item, e.g. 1.")]
        int id)
    {
        var wi = Store.GetById(id);
        return JsonSerializer.Serialize(WorkItemViewModels.Full(wi), JsonOptions.Default);
    }

    // -------------------------------------------------------------------------
    // CREATE
    // -------------------------------------------------------------------------

    [McpServerTool, Description(
        "Create a new work item. Returns the new ID and a summary. " +
        "Mock projects: 'FabrikamFiber' (types: Bug, Task, User Story, Feature, Epic, Test Case), " +
        "'AdventureWorks' (types: Bug, Task, Product Backlog Item, Feature, Impediment).")]
    public static string CreateWorkItem(
        [Description("Project name, e.g. 'FabrikamFiber'.")]
        string project,
        [Description("Work item type, e.g. 'Bug', 'Task', 'User Story'.")]
        string workItemType,
        [Description("Title / summary of the work item.")]
        string title,
        [Description("Optional longer description.")]
        string description = "",
        [Description("Optional assigned-to display name, e.g. 'Alice Johnson'.")]
        string assignedTo = "",
        [Description("Optional initial state. Defaults to the type's initial state.")]
        string state = "",
        [Description("Optional priority 1–4.")]
        int priority = 2)
    {
        var wi = Store.Create(new CreateWorkItemRequest
        {
            Project      = project,
            WorkItemType = workItemType,
            Title        = title,
            Description  = description,
            AssignedTo   = assignedTo,
            State        = state,
            Priority     = priority
        });

        return JsonSerializer.Serialize(WorkItemViewModels.Created(wi), JsonOptions.Default);
    }

    // -------------------------------------------------------------------------
    // UPDATE
    // -------------------------------------------------------------------------

    [McpServerTool, Description(
        "Update fields on an existing work item and optionally add a history comment. " +
        "Pass fieldsJson as a JSON object of field-reference-name → new-value pairs. " +
        "Example: {\"System.Title\":\"New title\",\"System.State\":\"Resolved\",\"System.AssignedTo\":\"Bob Smith\"}")]
    public static string UpdateWorkItem(
        [Description("The numeric ID of the work item to update.")]
        int id,
        [Description(
            "JSON object mapping TFS field reference names to new values. " +
            "E.g. {\"System.State\":\"Resolved\",\"System.AssignedTo\":\"Jane Doe\"}")]
        string fieldsJson,
        [Description("Optional comment to add to the history.")]
        string comment = "")
    {
        var fields = ParseFieldsJson(fieldsJson);
        var wi = Store.Update(id, fields, comment);
        return JsonSerializer.Serialize(WorkItemViewModels.Updated(wi, fields.Keys), JsonOptions.Default);
    }

    // -------------------------------------------------------------------------
    // LIST WORK ITEM TYPES
    // -------------------------------------------------------------------------

    [McpServerTool, Description(
        "List all work item types available in a project. " +
        "Mock projects: 'FabrikamFiber', 'AdventureWorks'.")]
    public static string ListWorkItemTypes(
        [Description("Project name, e.g. 'FabrikamFiber'.")]
        string project)
    {
        var types = Store.ListWorkItemTypes(project);
        return JsonSerializer.Serialize(WorkItemViewModels.TypeList(project, types), JsonOptions.Default);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Converts a JSON object string into a field-name → CLR-value dictionary.</summary>
    private static Dictionary<string, object?> ParseFieldsJson(string fieldsJson)
    {
        var rawFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fieldsJson)
            ?? throw new ArgumentException("'fieldsJson' must be a valid JSON object.");

        return rawFields.ToDictionary(
            kv => kv.Key,
            kv => (object?)(kv.Value.ValueKind switch
            {
                JsonValueKind.String => kv.Value.GetString(),
                JsonValueKind.Number => kv.Value.TryGetInt32(out int i) ? (object)i : kv.Value.GetDouble(),
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                _                    => kv.Value.ToString()
            }));
    }
}
