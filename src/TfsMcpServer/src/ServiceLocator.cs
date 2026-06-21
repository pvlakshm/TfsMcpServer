using Microsoft.Extensions.Logging;

namespace TfsMcpServer;

/// <summary>
/// Static accessor so that static [McpServerTool] methods can reach
/// shared services without constructor injection. The MCP SDK's
/// [McpServerTool] attribute requires static methods, which is why
/// this pragmatic compromise exists instead of full constructor DI.
/// Initialised once in Program.cs before the MCP server starts.
/// </summary>
public static class ServiceLocator
{
    private static IWorkItemStore? _workItemStore;

    public static IWorkItemStore WorkItemStore
    {
        get => _workItemStore ?? throw new InvalidOperationException(
            "ServiceLocator.WorkItemStore has not been initialised. " +
            "Call ServiceLocator.Initialise() in Program.cs first.");
    }

    public static void Initialise(TfsConfig config, ILoggerFactory loggerFactory)
    {
#if MOCK_ONLY
        // MockOnly build — TfsWorkItemStore and TfsConnectionFactory are not compiled.
        _workItemStore = new MockWorkItemStore(loggerFactory.CreateLogger<MockWorkItemStore>());
#else
        _workItemStore = config.AuthMode == AuthMode.Mock
            ? new MockWorkItemStore(loggerFactory.CreateLogger<MockWorkItemStore>())
            : new TfsWorkItemStore(
                new TfsConnectionFactory(config, loggerFactory.CreateLogger<TfsConnectionFactory>()),
                loggerFactory.CreateLogger<TfsWorkItemStore>());
#endif
    }
}
