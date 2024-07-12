#nullable enable

using System.Net;

using AdvancedUpdaterGitHubProxy.Models;

using Microsoft.Extensions.Caching.Memory;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

public class UpdatesEndpoint : Endpoint<UpdatesRequest>
{
    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromHours(1));

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

        UpdatesEndpointConfig? config = _config.GetSection("UpdatesEndpoint").Get<UpdatesEndpointConfig>();

        if (config is not null && config.BlacklistedUsernames.Contains(req.Username))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (config is not null && config.BlacklistedRepositories.Contains(req.Repository))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        IPAddress? remoteIpAddress = HttpContext.Request.HttpContext.Connection.RemoteIpAddress;

        // check for beta client
        bool isBetaClient = config?.BetaClients is not null &&
                            remoteIpAddress is not null &&
                            config.BetaClients.Contains(remoteIpAddress.ToString());

        switch (isBetaClient)
        {
            case true:
                _logger.LogWarning("Client {Remote} is beta client, bypassing cache and delivering pre-releases",
                    remoteIpAddress);
                break;
            // never deliver cached result to beta clients
            case false when _memoryCache.TryGetValue(req.ToString(), out Release? cached):
                {
                    // a 404 from the GH API was cached
                    if (cached is null)
                    {
                        _logger.LogDebug("Returning cached response for {Request} as Not Found", req.ToString());
                        await SendNotFoundAsync(ct);
                        return;
                    }

                    _logger.LogDebug("Returning cached response for {Request}", req.ToString());

                    if (asJson)
                    {
                        // original GH APi style payload has been requested
                        await SendOkAsync(cached, ct);
                    }
                    else
                    {
                        // deliver cached populated INI file content
                        await SendStringAsync(cached!.UpdaterInstructions!.ToString(), cancellation: ct);
                    }

                    return;
                }
        }

        _logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient client = _httpClientFactory.CreateClient("GitHub");

        List<Release>? response = await client.GetFromJsonAsync<List<Release>>(
            $"repos/{req.Username}/{req.Repository}/releases",
            ct
        );

        if (response is null)
        {
            _logger.LogDebug("No releases returned from GitHub API");
            await SendNotFoundAsync(ct);
            return;
        }

        IOrderedEnumerable<Release> releases = response.OrderByDescending(release => release.CreatedAt);

        Release? release = isBetaClient
            ? releases.FirstOrDefault(r => allowAny || r.BuildUpdaterInstructions())
            : releases.FirstOrDefault(r => allowAny || (!r.Prerelease && r.BuildUpdaterInstructions()));

        if (release is null)
        {
            _logger.LogDebug("No release with updater instructions found");
            _memoryCache.Set<Release?>(req.ToString(), null, CacheEntryOptions);
            await SendNotFoundAsync(ct);
            return;
        }

        release.UpdaterInstructions!.PopulateFileContent();

        _memoryCache.Set(req.ToString(), release, CacheEntryOptions);

        if (asJson)
        {
            await SendOkAsync(release, ct);
            return;
        }

        UpdaterInstructionsFile? instructions = release.UpdaterInstructions;

        if (instructions is null)
        {
            _logger.LogDebug("Selected release has no updater instructions");
            await SendNotFoundAsync(ct);
            return;
        }

        await SendStringAsync(instructions.ToString(), cancellation: ct);
    }
}