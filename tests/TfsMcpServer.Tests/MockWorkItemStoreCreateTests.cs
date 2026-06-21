using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class MockWorkItemStoreCreateTests
{
    private readonly MockWorkItemStore _store = new();

    [Fact]
    public void Create_ValidRequest_ReturnsNewWorkItemWithExpectedFields()
    {
        var wi = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Bug",
            Title = "New test bug",
            Description = "Something broke",
            AssignedTo = "Alice Johnson",
            Priority = 1
        });

        Assert.True(wi.Id > 0);
        Assert.Equal("New test bug", wi.Title);
        Assert.Equal("FabrikamFiber", wi.Project);
        Assert.Equal("Bug", wi.Type);
        Assert.Equal("Alice Johnson", wi.AssignedTo);
        Assert.Equal(1, wi.Priority);
        Assert.Equal("Active", wi.State); // default state for Bug
    }

    [Fact]
    public void Create_ThenGetById_ReturnsTheSameWorkItem()
    {
        var created = _store.Create(new CreateWorkItemRequest
        {
            Project = "AdventureWorks",
            WorkItemType = "Task",
            Title = "Round-trip test"
        });

        var fetched = _store.GetById(created.Id);

        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Round-trip test", fetched.Title);
    }

    [Fact]
    public void Create_TwoItems_AssignsSequentialUniqueIds()
    {
        var first = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber", WorkItemType = "Task", Title = "First"
        });
        var second = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber", WorkItemType = "Task", Title = "Second"
        });

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(second.Id > first.Id);
    }

    [Fact]
    public void Create_MissingTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Bug",
            Title = ""
        }));
    }

    [Fact]
    public void Create_UnknownProject_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _store.Create(new CreateWorkItemRequest
        {
            Project = "NoSuchProject",
            WorkItemType = "Bug",
            Title = "Doesn't matter"
        }));
        Assert.Contains("NoSuchProject", ex.Message);
    }

    [Fact]
    public void Create_UnknownWorkItemTypeForProject_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _store.Create(new CreateWorkItemRequest
        {
            Project = "AdventureWorks",
            WorkItemType = "Epic", // Epic is only valid in FabrikamFiber in the seed config
            Title = "Doesn't matter"
        }));
        Assert.Contains("Epic", ex.Message);
    }

    [Fact]
    public void Create_ExplicitState_OverridesDefaultState()
    {
        var wi = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Bug",
            Title = "Pre-resolved bug",
            State = "Resolved"
        });

        Assert.Equal("Resolved", wi.State);
    }
}
