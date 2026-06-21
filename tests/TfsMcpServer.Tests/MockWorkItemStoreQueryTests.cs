using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class MockWorkItemStoreQueryTests
{
    private readonly MockWorkItemStore _store = new();

    [Fact]
    public void Query_NoFilters_ReturnsAllSeedItemsOrderedByIdAscending()
    {
        var results = _store.Query("SELECT [System.Id] FROM WorkItems", maxResults: 200);

        Assert.Equal(10, results.Count);
        Assert.Equal(1, results.First().Id);
        Assert.Equal(10, results.Last().Id);
    }

    [Fact]
    public void Query_FilterByTeamProject_ReturnsOnlyMatchingProject()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'AdventureWorks'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.Equal(3, results.Count);
        Assert.All(results, wi => Assert.Equal("AdventureWorks", wi.Project));
    }

    [Fact]
    public void Query_FilterByWorkItemType_ReturnsOnlyMatchingType()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Bug'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.NotEmpty(results);
        Assert.All(results, wi => Assert.Equal("Bug", wi.Type));
    }

    [Fact]
    public void Query_FilterByStateEquals_ReturnsOnlyMatchingState()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.State] = 'Active'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.NotEmpty(results);
        Assert.All(results, wi => Assert.Equal("Active", wi.State));
    }

    [Fact]
    public void Query_FilterByStateNotEquals_ExcludesMatchingState()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.State] <> 'Active'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.NotEmpty(results);
        Assert.DoesNotContain(results, wi => wi.State == "Active");
    }

    [Fact]
    public void Query_FilterByAssignedTo_ReturnsOnlyMatchingAssignee()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.AssignedTo] = 'Alice Johnson'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.NotEmpty(results);
        Assert.All(results, wi => Assert.Equal("Alice Johnson", wi.AssignedTo));
    }

    [Fact]
    public void Query_CombinedFilters_AppliesAllConditions()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems " +
                   "WHERE [System.TeamProject] = 'FabrikamFiber' AND [System.WorkItemType] = 'Bug'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.All(results, wi =>
        {
            Assert.Equal("FabrikamFiber", wi.Project);
            Assert.Equal("Bug", wi.Type);
        });
    }

    [Fact]
    public void Query_OrderByIdDescending_ReturnsHighestIdFirst()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems ORDER BY [System.Id] DESC";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.Equal(10, results.First().Id);
        Assert.Equal(1, results.Last().Id);
    }

    [Fact]
    public void Query_MaxResults_LimitsReturnedCount()
    {
        var results = _store.Query("SELECT [System.Id] FROM WorkItems", maxResults: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Query_ProjectFilterIsCaseInsensitive()
    {
        var wiql = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'fabrikamfiber'";
        var results = _store.Query(wiql, maxResults: 200);

        Assert.NotEmpty(results);
        Assert.All(results, wi => Assert.Equal("FabrikamFiber", wi.Project));
    }
}
