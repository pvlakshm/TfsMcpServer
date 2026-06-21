namespace TfsMcpServer;

/// <summary>
/// Supported authentication strategies for connecting to TFS.
/// </summary>
public enum AuthMode
{
    /// <summary>NTLM pass-through using the current Windows user's credentials.</summary>
    Ntlm,

    /// <summary>Basic authentication with an explicit username and password.</summary>
    Basic,

    /// <summary>Personal Access Token (requires TFS 2013 Update 5+ or TFS Service).</summary>
    Pat,

    /// <summary>In-memory mock store — no TFS connection is made.</summary>
    Mock
}
