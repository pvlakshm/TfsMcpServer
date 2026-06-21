using TfsMcpServer;
using Xunit;

namespace TfsMcpServer.Tests;

public class TfsConfigParseAuthModeTests
{
    [Theory]
    [InlineData("ntlm", AuthMode.Ntlm)]
    [InlineData("NTLM", AuthMode.Ntlm)]
    [InlineData("Ntlm", AuthMode.Ntlm)]
    [InlineData("basic", AuthMode.Basic)]
    [InlineData("BASIC", AuthMode.Basic)]
    [InlineData("pat", AuthMode.Pat)]
    [InlineData("PAT", AuthMode.Pat)]
    [InlineData("mock", AuthMode.Mock)]
    [InlineData("MOCK", AuthMode.Mock)]
    [InlineData("Mock", AuthMode.Mock)]
    public void ParseAuthMode_RecognisedValue_ParsesCaseInsensitively(string raw, AuthMode expected)
    {
        var result = TfsConfig.ParseAuthMode(raw);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseAuthMode_EmptyString_DefaultsToNtlm()
    {
        Assert.Equal(AuthMode.Ntlm, TfsConfig.ParseAuthMode(""));
    }

    [Fact]
    public void ParseAuthMode_Whitespace_DefaultsToNtlm()
    {
        Assert.Equal(AuthMode.Ntlm, TfsConfig.ParseAuthMode("   "));
    }

    [Theory]
    [InlineData("nmtl")]      // the original typo this enum was designed to catch
    [InlineData("oauth")]
    [InlineData("kerberos")]
    [InlineData("123")]
    public void ParseAuthMode_UnrecognisedValue_ThrowsArgumentExceptionWithValidOptionsListed(string raw)
    {
        var ex = Assert.Throws<ArgumentException>(() => TfsConfig.ParseAuthMode(raw));

        Assert.Contains(raw, ex.Message);
        Assert.Contains("ntlm", ex.Message);
        Assert.Contains("basic", ex.Message);
        Assert.Contains("pat", ex.Message);
        Assert.Contains("mock", ex.Message);
    }
}
