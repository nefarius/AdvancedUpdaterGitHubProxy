namespace AdvancedUpdaterGitHubProxy.Models;

public sealed class UpdatesRequest
{
    /// <summary>
    ///     The GitHub user or organization name.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    ///     The GitHub repository name.
    /// </summary>
    public required string Repository { get; set; }

    public override string ToString()
    {
        return $"{Username}/{Repository}";
    }
}