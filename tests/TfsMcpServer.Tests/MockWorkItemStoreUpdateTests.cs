using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class MockWorkItemStoreUpdateTests
{
    private readonly MockWorkItemStore _store = new();

    [Fact]
    public void Update_TitleField_ChangesTitle()
    {
        var updated = _store.Update(
            id: 1,
            fields: new Dictionary<string, object?> { ["System.Title"] = "Updated title" },
            comment: "");

        Assert.Equal("Updated title", updated.Title);
    }

    [Fact]
    public void Update_StateField_ChangesState()
    {
        var updated = _store.Update(
            id: 1,
            fields: new Dictionary<string, object?> { ["System.State"] = "Resolved" },
            comment: "");

        Assert.Equal("Resolved", updated.State);
    }

    [Fact]
    public void Update_AssignedToField_ChangesAssignee()
    {
        var updated = _store.Update(
            id: 1,
            fields: new Dictionary<string, object?> { ["System.AssignedTo"] = "Eve Martinez" },
            comment: "");

        Assert.Equal("Eve Martinez", updated.AssignedTo);
    }

    [Fact]
    public void Update_MultipleFields_AppliesAllOfThem()
    {
        var updated = _store.Update(
            id: 2,
            fields: new Dictionary<string, object?>
            {
                ["System.State"] = "Closed",
                ["System.AssignedTo"] = "Dave Lee"
            },
            comment: "");

        Assert.Equal("Closed", updated.State);
        Assert.Equal("Dave Lee", updated.AssignedTo);
    }

    [Fact]
    public void Update_WithComment_SetsHistory()
    {
        var updated = _store.Update(
            id: 1,
            fields: new Dictionary<string, object?>(),
            comment: "Verified the fix in staging.");

        Assert.Equal("Verified the fix in staging.", updated.History);
    }

    [Fact]
    public void Update_RefreshesChangedByToMockUser()
    {
        var updated = _store.Update(
            id: 1,
            fields: new Dictionary<string, object?> { ["System.Title"] = "Touch" },
            comment: "");

        Assert.Equal("MockUser", updated.ChangedBy);
        Assert.True(updated.ChangedDate <= DateTime.UtcNow);
    }

    [Fact]
    public void Update_NonExistentId_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => _store.Update(
            id: 99999,
            fields: new Dictionary<string, object?> { ["System.Title"] = "x" },
            comment: ""));
    }

    [Fact]
    public void Update_UnknownFieldName_IsStoredInGenericFieldsBag()
    {
        var updated = _store.Update(
            id: 1,
            fields: new Dictionary<string, object?> { ["Custom.MyField"] = "custom-value" },
            comment: "");

        Assert.Equal("custom-value", updated.Fields["Custom.MyField"]);
    }
}
