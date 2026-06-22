using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class MockWorkItemStoreParentChildTests
{
    private readonly MockWorkItemStore _store = new();

    [Fact]
    public void Create_WithParentId_SetsParentIdOnChild()
    {
        // Seed item #6 is an existing User Story in FabrikamFiber.
        var child = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Task",
            Title = "Implement password reset email sending",
            ParentId = 6
        });

        Assert.Equal(6, child.ParentId);
    }

    [Fact]
    public void Create_WithParentId_AddsChildToParentsChildIds()
    {
        var child = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Task",
            Title = "Implement password reset email sending",
            ParentId = 6
        });

        var parent = _store.GetById(6);
        Assert.Contains(child.Id, parent.ChildIds);
    }

    [Fact]
    public void Create_WithoutParentId_LeavesParentIdNull()
    {
        var wi = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Bug",
            Title = "Standalone bug, no parent"
        });

        Assert.Null(wi.ParentId);
    }

    [Fact]
    public void Create_WithZeroParentId_TreatedAsNoParent()
    {
        var wi = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Bug",
            Title = "Zero parent means no parent",
            ParentId = 0
        });

        Assert.Null(wi.ParentId);
    }

    [Fact]
    public void Create_WithNonExistentParentId_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Task",
            Title = "Orphan task with bad parent",
            ParentId = 99999
        }));

        Assert.Contains("99999", ex.Message);
    }

    [Fact]
    public void Create_WithNonExistentParentId_DoesNotCreateTheChildEither()
    {
        var countBefore = _store.Query("SELECT [System.Id] FROM WorkItems", maxResults: 200).Count;

        Assert.Throws<ArgumentException>(() => _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Task",
            Title = "Should not be created",
            ParentId = 99999
        }));

        var countAfter = _store.Query("SELECT [System.Id] FROM WorkItems", maxResults: 200).Count;
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void Create_MultipleChildrenUnderSameParent_AllAppearInParentsChildIds()
    {
        var child1 = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber", WorkItemType = "Task", Title = "Subtask 1", ParentId = 7
        });
        var child2 = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber", WorkItemType = "Task", Title = "Subtask 2", ParentId = 7
        });

        var parent = _store.GetById(7);
        Assert.Contains(child1.Id, parent.ChildIds);
        Assert.Contains(child2.Id, parent.ChildIds);
        Assert.Equal(2, parent.ChildIds.Count);
    }

    [Fact]
    public void Create_ChildAcrossDifferentTypes_FeatureParentOfUserStory()
    {
        // First create a Feature to act as parent.
        var feature = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "Feature",
            Title = "Account self-service"
        });

        var story = _store.Create(new CreateWorkItemRequest
        {
            Project = "FabrikamFiber",
            WorkItemType = "User Story",
            Title = "As a user I can update my profile photo",
            ParentId = feature.Id
        });

        Assert.Equal(feature.Id, story.ParentId);

        var refetchedFeature = _store.GetById(feature.Id);
        Assert.Contains(story.Id, refetchedFeature.ChildIds);
    }
}
