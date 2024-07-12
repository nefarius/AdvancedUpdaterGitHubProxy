using System.Diagnostics.CodeAnalysis;

namespace AdvancedUpdaterGitHubProxy.Endpoints.AssetsEndpoint;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class AssetsRequest
{
    /// <summary>
    ///     The GitHub user or organization name.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>
    ///     The GitHub repository name.
    /// </summary>
    public string Repository { get; set; } = default!;

    /// <summary>
    ///     Optional architecture. Valid values include: x86, x64 and arm64.
    /// </summary>
    public string Architecture { get; set; } = default!;

    /// <summary>
    ///     Optional filename the response should use. Some clients can't handle the URL not ending with a "real" file name, so
    ///     this value will be reflected in the response.
    /// </summary>
    public string Filename { get; set; } = default!;

    public override string ToString()
    {
        return $"{Username}/{Repository}";
    }
}