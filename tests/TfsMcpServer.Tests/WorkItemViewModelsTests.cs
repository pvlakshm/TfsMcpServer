using System.Text.Json;
using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class WorkItemViewModelsTests
{
    private static WorkItemData SampleWorkItem() => new()
    {
        Id = 42,
        Type = "Bug",
        Project = "FabrikamFiber",
        Title = "Sample bug",
        State = "Active",
        AssignedTo = "Alice Johnson",
        CreatedBy = "Bob Smith",
        ChangedBy = "Alice Johnson",
        CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ChangedDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        Description = "Something is broken",
        History = "Investigating",
        Priority = 1,
        Url = "http://mock-tfs/tfs/FabrikamFiber/_workitems/edit/42",
        Fields = new Dictionary<string, object?> { ["System.Title"] = "Sample bug" }
    };

    [Fact]
    public void Brief_IncludesOnlySummaryFields()
    {
        var view = WorkItemViewModels.Brief(SampleWorkItem());
        var json = JsonSerializer.Serialize(view);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("Id", out _));
        Assert.True(doc.RootElement.TryGetProperty("Title", out _));
        Assert.True(doc.RootElement.TryGetProperty("State", out _));
        // Brief view should NOT include the full description or field bag
        Assert.False(doc.RootElement.TryGetProperty("Description", out _));
        Assert.False(doc.RootElement.TryGetProperty("Fields", out _));
    }

    [Fact]
    public void Full_IncludesDescriptionAndFieldsBag()
    {
        var view = WorkItemViewModels.Full(SampleWorkItem());
        var json = JsonSerializer.Serialize(view);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("Description", out var desc));
        Assert.Equal("Something is broken", desc.GetString());
        Assert.True(doc.RootElement.TryGetProperty("Fields", out _));
    }

    [Fact]
    public void Created_ReturnsSuccessTrueAndIncludesId()
    {
        var view = WorkItemViewModels.Created(SampleWorkItem());
        var json = JsonSerializer.Serialize(view);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(42, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Contains("42", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Updated_IncludesUpdatedFieldNames()
    {
        var view = WorkItemViewModels.Updated(SampleWorkItem(), new[] { "System.State", "System.AssignedTo" });
        var json = JsonSerializer.Serialize(view);
        var doc = JsonDocument.Parse(json);

        var fields = doc.RootElement.GetProperty("updatedFields").EnumerateArray()
            .Select(e => e.GetString()).ToArray();

        Assert.Contains("System.State", fields);
        Assert.Contains("System.AssignedTo", fields);
    }

    [Fact]
    public void QueryResult_ReportsCorrectTotalReturned()
    {
        var items = new List<WorkItemData> { SampleWorkItem(), SampleWorkItem() };
        var view = WorkItemViewModels.QueryResult(items);
        var json = JsonSerializer.Serialize(view);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(2, doc.RootElement.GetProperty("totalReturned").GetInt32());
    }

    [Fact]
    public void TypeList_IncludesProjectNameAndTypes()
    {
        var view = WorkItemViewModels.TypeList("FabrikamFiber", new[] { "Bug", "Task" });
        var json = JsonSerializer.Serialize(view);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("FabrikamFiber", doc.RootElement.GetProperty("project").GetString());
        var types = doc.RootElement.GetProperty("workItemTypes").EnumerateArray()
            .Select(e => e.GetString()).ToArray();
        Assert.Contains("Bug", types);
        Assert.Contains("Task", types);
    }
}
