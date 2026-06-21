using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMcpServer;

/// <summary>
/// Real TFS implementation of <see cref="IWorkItemStore"/>.
/// Delegates to the TFS Client Object Model via <see cref="TfsConnectionFactory"/>.
/// </summary>
public sealed class TfsWorkItemStore : IWorkItemStore
{
    private readonly TfsConnectionFactory _factory;
    private readonly ILogger<TfsWorkItemStore> _logger;

    public TfsWorkItemStore(TfsConnectionFactory factory, ILogger<TfsWorkItemStore> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public IReadOnlyList<WorkItemData> Query(string wiql, int maxResults)
    {
        _logger.LogInformation("Running WIQL query (maxResults={MaxResults}): {Wiql}", maxResults, wiql);
        var store = _factory.GetWorkItemStore();
        var query = new Query(store, wiql);
        var items = query.RunQuery();

        var results = new List<WorkItemData>();
        int count = 0;
        foreach (WorkItem wi in items)
        {
            if (count++ >= maxResults) break;
            results.Add(Map(wi));
        }
        _logger.LogInformation("Query returned {Count} work item(s)", results.Count);
        return results;
    }

    public WorkItemData GetById(int id)
    {
        _logger.LogInformation("Fetching work item #{Id}", id);
        var store = _factory.GetWorkItemStore();
        return Map(store.GetWorkItem(id));
    }

    public WorkItemData Create(CreateWorkItemRequest req)
    {
        _logger.LogInformation(
            "Creating {WorkItemType} '{Title}' in project {Project}",
            req.WorkItemType, req.Title, req.Project);

        var store = _factory.GetWorkItemStore();

        var project = store.Projects[req.Project]
            ?? throw new ArgumentException($"Project '{req.Project}' not found.");

        var witd = project.WorkItemTypes[req.WorkItemType]
            ?? throw new ArgumentException(
                $"Work item type '{req.WorkItemType}' not found in '{req.Project}'.");

        var wi = new WorkItem(witd) { Title = req.Title };

        if (!string.IsNullOrWhiteSpace(req.Description)) wi.Description = req.Description;
        if (!string.IsNullOrWhiteSpace(req.AssignedTo))  wi.Fields[CoreField.AssignedTo].Value = req.AssignedTo;
        if (!string.IsNullOrWhiteSpace(req.State))       wi.State = req.State;
        if (req.Priority > 0 && wi.Fields.Contains("Priority")) wi.Fields["Priority"].Value = req.Priority;

        ValidateOrThrow(wi);
        wi.Save();
        _logger.LogInformation("Created work item #{Id}", wi.Id);
        return Map(wi);
    }

    public WorkItemData Update(int id, Dictionary<string, object?> fields, string comment)
    {
        _logger.LogInformation("Updating work item #{Id}, fields: {Fields}", id, string.Join(", ", fields.Keys));
        var store = _factory.GetWorkItemStore();
        var wi = store.GetWorkItem(id);

        foreach (var (key, value) in fields)
        {
            if (!wi.Fields.Contains(key))
                throw new ArgumentException($"Field '{key}' does not exist on work item #{id}.");
            wi.Fields[key].Value = value;
        }

        if (!string.IsNullOrWhiteSpace(comment)) wi.History = comment;

        ValidateOrThrow(wi);
        wi.Save();
        _logger.LogInformation("Updated work item #{Id}", wi.Id);
        return Map(wi);
    }

    public IReadOnlyList<string> ListWorkItemTypes(string project)
    {
        _logger.LogInformation("Listing work item types for project {Project}", project);
        var store = _factory.GetWorkItemStore();
        var proj = store.Projects[project]
            ?? throw new ArgumentException($"Project '{project}' not found.");

        var types = new List<string>();
        foreach (WorkItemType t in proj.WorkItemTypes) types.Add(t.Name);
        return types;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static WorkItemData Map(WorkItem wi)
    {
        var allFields = new Dictionary<string, object?>();
        foreach (Field f in wi.Fields) allFields[f.ReferenceName] = f.Value;

        return new WorkItemData
        {
            Id          = wi.Id,
            Type        = wi.Type.Name,
            Project     = wi.Project.Name,
            Title       = wi.Title,
            State       = wi.State,
            AssignedTo  = wi.Fields.Contains(CoreField.AssignedTo)
                            ? wi.Fields[CoreField.AssignedTo].Value?.ToString() ?? ""
                            : "",
            CreatedBy   = wi.CreatedBy,
            ChangedBy   = wi.ChangedBy,
            CreatedDate = wi.CreatedDate,
            ChangedDate = wi.ChangedDate,
            Description = wi.Description,
            History     = wi.History,
            Priority    = wi.Fields.Contains("Priority")
                            ? Convert.ToInt32(wi.Fields["Priority"].Value)
                            : 0,
            Url         = wi.Uri.ToString(),
            Fields      = allFields
        };
    }

    private static void ValidateOrThrow(WorkItem wi)
    {
        var errors = wi.Validate();
        if (errors.Count == 0) return;

        var msgs = new StringBuilder();
        foreach (Field f in errors) msgs.AppendLine($"  [{f.Name}]: {f.Status}");
        throw new InvalidOperationException($"Work item validation failed:\n{msgs}");
    }
}
