using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class MockWorkItemStoreGetByIdTests
{
    private readonly MockWorkItemStore _store = new();

    [Fact]
    public void GetById_ExistingId_ReturnsWorkItem()
    {
        var wi = _store.GetById(1);

        Assert.Equal(1, wi.Id);
        Assert.Equal("Bug", wi.Type);
        Assert.Equal("FabrikamFiber", wi.Project);
        Assert.Equal("Login page crashes on invalid email format", wi.Title);
    }

    [Fact]
    public void GetById_PopulatesFieldsDictionary()
    {
        var wi = _store.GetById(1);

        Assert.NotEmpty(wi.Fields);
        Assert.Equal(wi.Title, wi.Fields["System.Title"]);
        Assert.Equal(wi.State, wi.Fields["System.State"]);
    }

    [Fact]
    public void GetById_NonExistentId_ThrowsKeyNotFoundException()
    {
        var ex = Assert.Throws<KeyNotFoundException>(() => _store.GetById(99999));
        Assert.Contains("99999", ex.Message);
    }
}
