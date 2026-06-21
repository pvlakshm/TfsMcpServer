using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TfsMcpServer;

/// <summary>
/// Creates and caches a <see cref="TfsTeamProjectCollection"/> for the lifetime
/// of the process. Supports NTLM (pass-through), Basic, and PAT authentication.
/// </summary>
public sealed class TfsConnectionFactory : IDisposable
{
    private readonly TfsConfig _config;
    private readonly ILogger<TfsConnectionFactory> _logger;
    private TfsTeamProjectCollection? _collection;
    private readonly Lock _lock = new();

    public TfsConnectionFactory(TfsConfig config, ILogger<TfsConnectionFactory> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>Returns a connected, authenticated <see cref="TfsTeamProjectCollection"/>.</summary>
    public TfsTeamProjectCollection GetCollection()
    {
        lock (_lock)
        {
            if (_collection is not null)
                return _collection;

            if (string.IsNullOrWhiteSpace(_config.CollectionUrl))
                throw new InvalidOperationException(
                    "TFS_COLLECTION_URL is not configured. " +
                    "Set it as an environment variable before starting the server.");

            var uri = new Uri(_config.CollectionUrl);
            _logger.LogInformation("Connecting to TFS collection {Uri} using {AuthMode} auth", uri, _config.AuthMode);

            TfsTeamProjectCollection collection = _config.AuthMode switch
            {
                AuthMode.Ntlm => new TfsTeamProjectCollection(uri, CredentialCache.DefaultNetworkCredentials),

                AuthMode.Basic => new TfsTeamProjectCollection(
                    uri,
                    new NetworkCredential(_config.Username, _config.Password)),

                // For TFS 2013 PAT support you need TFS 2013 Update 5+ or Team Foundation Service.
                // A PAT is sent as Basic auth with an empty username.
                AuthMode.Pat => new TfsTeamProjectCollection(
                    uri,
                    new NetworkCredential(string.Empty, _config.Password)),

                AuthMode.Mock => throw new InvalidOperationException(
                    "TfsConnectionFactory should not be used when AuthMode is Mock. " +
                    "This indicates a wiring bug in ServiceLocator."),

                _ => throw new InvalidOperationException(
                    $"Unhandled AuthMode '{_config.AuthMode}'.")
            };

            collection.EnsureAuthenticated();
            _collection = collection;
            _logger.LogInformation("TFS connection established successfully");
            return _collection;
        }
    }

    /// <summary>Convenience helper to get the Work Item Store.</summary>
    public WorkItemStore GetWorkItemStore()
        => GetCollection().GetService<WorkItemStore>();

    public void Dispose() => _collection?.Dispose();
}
