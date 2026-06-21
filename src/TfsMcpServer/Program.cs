using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TfsMcpServer;
using TfsMcpServer.Tools;

// ---------------------------------------------------------------------------
// 1. Build the host first so we have a real ILoggerFactory to use everywhere,
//    including during configuration validation below.
// ---------------------------------------------------------------------------
var builder = Host.CreateApplicationBuilder(args);

builder.Logging
    .ClearProviders()
    // Log to stderr so MCP JSON-RPC traffic on stdout stays clean.
    .AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)
    .SetMinimumLevel(LogLevel.Information);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(WorkItemTools).Assembly);

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TfsMcpServer.Startup");

// ---------------------------------------------------------------------------
// 2. Load and validate configuration from environment variables
// ---------------------------------------------------------------------------
AuthMode authMode;
try
{
    authMode = TfsConfig.ParseAuthMode(Env("TFS_AUTH_MODE", "ntlm"));
}
catch (ArgumentException ex)
{
    logger.LogCritical("{Message}", ex.Message);
    return 1;
}

var config = new TfsConfig
{
    CollectionUrl  = Env("TFS_COLLECTION_URL"),
    AuthMode       = authMode,
    Username       = Env("TFS_USERNAME"),
    Password       = Env("TFS_PASSWORD"),
    DefaultProject = Env("TFS_DEFAULT_PROJECT")
};

// Require a collection URL only when connecting to a real TFS instance.
if (config.AuthMode != AuthMode.Mock && string.IsNullOrWhiteSpace(config.CollectionUrl))
{
    logger.LogCritical(
        "TFS_COLLECTION_URL is not set. Set it to your TFS collection, e.g. " +
        "TFS_COLLECTION_URL=http://tfs2013:8080/tfs/DefaultCollection — " +
        "or run in mock mode for testing without TFS: TFS_AUTH_MODE=mock");
    return 1;
}

// ---------------------------------------------------------------------------
// 3. Initialise the work item store (mock or real TFS)
// ---------------------------------------------------------------------------
ServiceLocator.Initialise(config, app.Services.GetRequiredService<ILoggerFactory>());

logger.LogInformation(
    config.AuthMode == AuthMode.Mock
        ? "TFS MCP Server starting in MOCK mode — no TFS connection required."
        : "TFS MCP Server starting — collection: {CollectionUrl}, auth: {AuthMode}",
    config.CollectionUrl, config.AuthMode);

// ---------------------------------------------------------------------------
// 4. Run the MCP server (stdio transport)
// ---------------------------------------------------------------------------
await app.RunAsync();
return 0;

static string Env(string name, string fallback = "") =>
    Environment.GetEnvironmentVariable(name) ?? fallback;
