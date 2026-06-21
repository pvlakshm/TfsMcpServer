namespace TfsMcpServer;

/// <summary>
/// Loaded from environment variables. See <see cref="TryParseAuthMode"/>
/// for how the raw "TFS_AUTH_MODE" string is validated.
/// </summary>
public sealed class TfsConfig
{
    /// <summary>
    /// Full TFS collection URL, e.g. http://tfs2013:8080/tfs/DefaultCollection
    /// </summary>
    public string CollectionUrl { get; init; } = string.Empty;

    /// <summary>How to authenticate to TFS. Defaults to <see cref="AuthMode.Ntlm"/>.</summary>
    public AuthMode AuthMode { get; init; } = AuthMode.Ntlm;

    /// <summary>Used when AuthMode is Basic or Pat.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Password (Basic) or Personal Access Token (Pat).</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Default TFS project name used when a tool call omits it.</summary>
    public string DefaultProject { get; init; } = string.Empty;

    /// <summary>
    /// Parses a raw env-var string (e.g. "ntlm", "Mock", "PAT") into an <see cref="AuthMode"/>.
    /// Throws a clear, actionable error on an unrecognised value instead of silently
    /// falling through at TFS-connection time.
    /// </summary>
    public static AuthMode ParseAuthMode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AuthMode.Ntlm;

        // Enum.TryParse accepts numeric strings too (e.g. "123" → (AuthMode)123),
        // even when no member has that value. Enum.IsDefined rejects those so a
        // typo like a stray digit fails loudly instead of becoming a bogus AuthMode.
        if (Enum.TryParse<AuthMode>(raw, ignoreCase: true, out var mode) && Enum.IsDefined(mode))
            return mode;

        var valid = string.Join(", ", Enum.GetNames<AuthMode>().Select(n => n.ToLowerInvariant()));
        throw new ArgumentException(
            $"Unrecognised TFS_AUTH_MODE value '{raw}'. Valid values: {valid}.");
    }
}
