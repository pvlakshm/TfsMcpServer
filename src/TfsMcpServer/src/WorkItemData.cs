namespace TfsMcpServer;

/// <summary>
/// Transport model representing a TFS work item.
/// Returned by all <see cref="IWorkItemStore"/> read operations.
/// </summary>
public sealed class WorkItemData
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Project { get; set; } = "";
    public string Title { get; set; } = "";
    public string State { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string ChangedBy { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public DateTime ChangedDate { get; set; }
    public string Description { get; set; } = "";
    public string History { get; set; } = "";
    public int Priority { get; set; } = 2;
    public string Url { get; set; } = "";
    public Dictionary<string, object?> Fields { get; set; } = [];
}
