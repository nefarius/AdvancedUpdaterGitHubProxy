#nullable enable
using System.Net;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

public sealed class UpdatesEndpointConfig
{
    public List<string>? BetaClients { get; set; }
}