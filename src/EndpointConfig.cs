#nullable enable
using System.Net;

namespace AdvancedUpdaterGitHubProxy;

public sealed class UpdatesEndpointConfig
{
    /// <summary>
    ///     Optional collection of IP addresses that are allowed to bypass the cache to get an instant response.
    /// </summary>
    public List<IPAddress>? BetaClients { get; set; }

    /// <summary>
    ///     Optional collection of usernames that should not be looked up in the GitHub API.
    /// </summary>
    public List<string> BlacklistedUsernames { get; set; } = new();

    /// <summary>
    ///     Optional collection of repositories that should not be looked up in the GitHub API.
    /// </summary>
    public List<string> BlacklistedRepositories { get; set; } = new();
}