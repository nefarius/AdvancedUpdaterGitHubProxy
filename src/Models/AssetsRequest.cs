using System.Diagnostics.CodeAnalysis;

namespace AdvancedUpdaterGitHubProxy.Models;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class AssetsRequest
{
    /// <summary>
    ///     The GitHub user or organization name.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    ///     The GitHub repository name.
    /// </summary>
    public required string Repository { get; set; }

    /// <summary>
    ///     Optional architecture. Valid values include: x86, x64 and arm64.
    /// </summary>
    public required string Architecture { get; set; }

    /// <summary>
    ///     Optional filename the response should use. Some clients can't handle the URL not ending with a "real" file name, so
    ///     this value will be reflected in the response.
    /// </summary>
    public required string Filename { get; set; }

    public override string ToString()
    {
        return $"{Username}/{Repository}";
    }
}