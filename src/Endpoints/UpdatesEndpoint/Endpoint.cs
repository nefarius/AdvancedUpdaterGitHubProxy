using System.Net;

using AdvancedUpdaterGitHubProxy.Models;

using Microsoft.Extensions.Caching.Memory;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

public class UpdatesEndpoint : Endpoint<UpdatesRequest>
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdatesEndpoint> _logger;
    private readonly IMemoryCache _memoryCache;

    public UpdatesEndpoint(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache,
        ILogger<UpdatesEndpoint> logger, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _config = config;
    }

    public override void Configure()
    {
        Get("/api/github/{Username}/{Repository}/updates");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Returns Advanced Installer Updater Configuration";
            s.Description =
                "Contacts the GitHub API, fetches the latest public release and transforms it into an Advanced Installer Updater compatible INI configuration file.";
            s.Responses[200] = "The updater configuration was returned successfully.";
            s.Responses[404] = "No public release was found.";
        });
    }

    public override async Task HandleAsync(UpdatesRequest req, CancellationToken ct)
    {
        bool asJson = Query<bool>("asJson", false);
        bool allowAny = Query<bool>("allowAny", false);

        UpdatesEndpointConfig config = _config.GetSection("UpdatesEndpoint").Get<UpdatesEndpointConfig>();

        if (config.BlacklistedUsernames.Contains(req.Username))
        {
            await SendNotFoundAsync(ct);
            return;
        }
        
        if (config.BlacklistedRepositories.Contains(req.Repository))
        {
            await SendNotFoundAsync(ct);
            return;
        }
        
        IPAddress remoteIpAddress = HttpContext.Request.HttpContext.Connection.RemoteIpAddress;

        // check for beta client
        bool isBetaClient = config?.BetaClients is not null &&
                            remoteIpAddress is not null &&
                            config.BetaClients.Contains(remoteIpAddress.ToString());

        if (isBetaClient)
        {
            _logger.LogWarning("Client {Remote} is beta client, bypassing cache and delivering pre-releases",
                remoteIpAddress);
        }

        // never deliver cached result to beta clients
        if (!isBetaClient && _memoryCache.TryGetValue(req.ToString()!, out Models.Release cached))
        {
            _logger.LogDebug("Returning cached response for {Request}", req.ToString());

            if (asJson)
            {
                await SendOkAsync(cached, ct);
            }
            else
            {
                await SendStringAsync(cached.UpdaterInstructions.ToString()!, cancellation: ct);
            }

            return;
        }

        _logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient client = _httpClientFactory.CreateClient("GitHub");

        List<Models.Release> response = await client.GetFromJsonAsync<List<Models.Release>>(
            $"repos/{req.Username}/{req.Repository}/releases",
            ct
        );

        if (response is null)
        {
            _logger.LogDebug("No releases returned from GitHub API");
            await SendNotFoundAsync(ct);
            return;
        }

        IOrderedEnumerable<Models.Release> releases = response.OrderByDescending(release => release.CreatedAt);

        Models.Release release = isBetaClient
            ? releases.FirstOrDefault(r => allowAny || r.UpdaterInstructions is not null)
            : releases.FirstOrDefault(r => allowAny || (!r.Prerelease && r.UpdaterInstructions is not null));

        if (release is null)
        {
            _logger.LogDebug("No release with updater instructions found");
            await SendNotFoundAsync(ct);
            return;
        }

        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(1));

        _memoryCache.Set(req.ToString()!, release, cacheEntryOptions);

        if (asJson)
        {
            await SendOkAsync(release, ct);
            return;
        }

        UpdaterInstructionsFile instructions = release.UpdaterInstructions;

        if (instructions is null)
        {
            _logger.LogDebug("Selected release has no updater instructions");
            await SendNotFoundAsync(ct);
            return;
        }

        await SendStringAsync(instructions.ToString()!, cancellation: ct);
    }
}