using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class MockWorkItemStoreListWorkItemTypesTests
{
    private readonly MockWorkItemStore _store = new();

    [Fact]
    public void ListWorkItemTypes_KnownProject_ReturnsExpectedTypes()
    {
        var types = _store.ListWorkItemTypes("FabrikamFiber");

        Assert.Contains("Bug", types);
        Assert.Contains("Task", types);
        Assert.Contains("User Story", types);
    }

    [Fact]
    public void ListWorkItemTypes_IsCaseInsensitiveOnProjectName()
    {
        var types = _store.ListWorkItemTypes("adventureworks");

        Assert.NotEmpty(types);
        Assert.Contains("Product Backlog Item", types);
    }

    [Fact]
    public void ListWorkItemTypes_UnknownProject_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _store.ListWorkItemTypes("DoesNotExist"));
        Assert.Contains("DoesNotExist", ex.Message);
    }
}
