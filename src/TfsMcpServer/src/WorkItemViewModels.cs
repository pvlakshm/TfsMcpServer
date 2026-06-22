namespace TfsMcpServer;

/// <summary>
/// Shapes <see cref="WorkItemData"/> into the JSON views returned by MCP tools.
/// Kept separate from <see cref="Tools.WorkItemTools"/> so tool methods stay focused
/// on orchestration (call the store, return a result) rather than response formatting.
/// </summary>
public static class WorkItemViewModels
{
    /// <summary>Compact view used in query result lists.</summary>
    public static object Brief(WorkItemData wi) => new
    {
        wi.Id,
        wi.Type,
        wi.Title,
        wi.State,
        wi.AssignedTo,
        wi.ChangedDate
    };

    /// <summary>Full view used when fetching a single work item by ID.</summary>
    public static object Full(WorkItemData wi) => new
    {
        wi.Id,
        wi.Type,
        wi.Project,
        wi.Title,
        wi.State,
        wi.AssignedTo,
        wi.CreatedBy,
        wi.ChangedBy,
        wi.CreatedDate,
        wi.ChangedDate,
        wi.Description,
        wi.History,
        wi.Priority,
        wi.Url,
        wi.ParentId,
        wi.ChildIds,
        wi.Fields
    };

    /// <summary>Response returned after a successful create.</summary>
    public static object Created(WorkItemData wi) => new
    {
        success = true,
        id = wi.Id,
        parentId = wi.ParentId,
        url = wi.Url,
        message = wi.ParentId is int p
            ? $"Work item #{wi.Id} '{wi.Title}' created successfully as a child of #{p}."
            : $"Work item #{wi.Id} '{wi.Title}' created successfully."
    };

    /// <summary>Response returned after a successful update.</summary>
    public static object Updated(WorkItemData wi, IEnumerable<string> updatedFields) => new
    {
        success = true,
        id = wi.Id,
        updatedFields = updatedFields.ToArray(),
        message = $"Work item #{wi.Id} updated successfully."
    };

    /// <summary>Response for a query result list.</summary>
    public static object QueryResult(IReadOnlyList<WorkItemData> items) => new
    {
        totalReturned = items.Count,
        workItems = items.Select(Brief)
    };

    /// <summary>Response for a work item type listing.</summary>
    public static object TypeList(string project, IReadOnlyList<string> types) => new
    {
        project,
        workItemTypes = types
    };
}
