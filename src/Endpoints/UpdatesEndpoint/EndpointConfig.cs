#nullable enable
using System.Diagnostics.CodeAnalysis;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public sealed class UpdatesEndpointConfig
{
    public List<string>? BetaClients { get; set; }
}