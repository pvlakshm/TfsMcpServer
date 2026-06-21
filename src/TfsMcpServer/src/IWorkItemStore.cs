namespace TfsMcpServer;

/// <summary>
/// Abstraction over TFS Work Item operations.
/// Implemented by <see cref="TfsWorkItemStore"/> (real) and
/// <see cref="MockWorkItemStore"/> (in-memory, for testing without TFS).
/// </summary>
public interface IWorkItemStore
{
    /// <summary>Run a WIQL query and return matching work items (up to <paramref name="maxResults"/>).</summary>
    IReadOnlyList<WorkItemData> Query(string wiql, int maxResults);

    /// <summary>Fetch a single work item by ID.</summary>
    WorkItemData GetById(int id);

    /// <summary>Create a new work item and return the saved result.</summary>
    WorkItemData Create(CreateWorkItemRequest request);

    /// <summary>Update fields on an existing work item and return the updated result.</summary>
    WorkItemData Update(int id, Dictionary<string, object?> fields, string comment);

    /// <summary>List all work item type names available in a project.</summary>
    IReadOnlyList<string> ListWorkItemTypes(string project);
}
