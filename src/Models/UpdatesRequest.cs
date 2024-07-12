using System.Diagnostics.CodeAnalysis;

namespace AdvancedUpdaterGitHubProxy.Models;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class UpdatesRequest
{
    /// <summary>
    ///     The GitHub user or organization name.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>
    ///     The GitHub repository name.
    /// </summary>
    public string Repository { get; set; } = default!;

    public override string ToString()
    {
        return $"{Username}/{Repository}";
    }
}