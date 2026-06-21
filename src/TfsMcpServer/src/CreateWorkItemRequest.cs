namespace TfsMcpServer;

/// <summary>
/// Parameter object for <see cref="IWorkItemStore.Create"/>.
/// </summary>
public sealed class CreateWorkItemRequest
{
    public string Project { get; init; } = "";
    public string WorkItemType { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string AssignedTo { get; init; } = "";
    public string State { get; init; } = "";
    public int Priority { get; init; } = 2;
}
