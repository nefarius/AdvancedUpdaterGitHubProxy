#nullable enable

using System.Net;

using AdvancedUpdaterGitHubProxy.Models;

using Microsoft.Extensions.Caching.Memory;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

public class UpdatesEndpoint(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    ILogger<UpdatesEndpoint> logger,
    IConfiguration config)
    : Endpoint<UpdatesRequest>
{
    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromHours(1));

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

        UpdatesEndpointConfig? config1 = config.GetSection("UpdatesEndpoint").Get<UpdatesEndpointConfig>();

        if (config1 is not null && config1.BlacklistedUsernames.Contains(req.Username))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (config1 is not null && config1.BlacklistedRepositories.Contains(req.Repository))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        IPAddress? remoteIpAddress = HttpContext.Request.HttpContext.Connection.RemoteIpAddress;

        // check for beta client
        bool isBetaClient = config1?.BetaClients is not null &&
                            remoteIpAddress is not null &&
                            config1.BetaClients.Contains(remoteIpAddress);

        switch (isBetaClient)
        {
            case true:
                logger.LogWarning("Client {Remote} is beta client, bypassing cache and delivering pre-releases",
                    remoteIpAddress);
                break;
            // never deliver cached result to beta clients
            case false when memoryCache.TryGetValue(req.ToString(), out Release? cached):
                {
                    // a 404 from the GH API was cached
                    if (cached is null)
                    {
                        logger.LogDebug("Returning cached response for {Request} as Not Found", req.ToString());
                        await Send.NotFoundAsync(ct);
                        return;
                    }

                    logger.LogDebug("Returning cached response for {Request}", req.ToString());

                    if (asJson)
                    {
                        // original GH APi style payload has been requested
                        await Send.OkAsync(cached, ct);
                    }
                    else
                    {
                        // deliver cached populated INI file content
                        await Send.StringAsync(cached!.UpdaterInstructions!.ToString(), cancellation: ct);
                    }

                    return;
                }
        }

        logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient client = httpClientFactory.CreateClient("GitHub");

        List<Release>? response = await client.GetFromJsonAsync<List<Release>>(
            $"repos/{req.Username}/{req.Repository}/releases",
            ct
        );

        if (response is null)
        {
            logger.LogDebug("No releases returned from GitHub API");
            await Send.NotFoundAsync(ct);
            return;
        }

        List<Release> releases = response.OrderByDescending(release => release.CreatedAt).ToList();

        Release? releaseWithInfo = isBetaClient
            ? releases.FirstOrDefault(r => allowAny || r.EnsureUpdaterInstructions() is not null)
            : releases.FirstOrDefault(r => allowAny || (!r.Prerelease && r.EnsureUpdaterInstructions() is not null));

        if (releaseWithInfo is null)
        {
            if (asJson || allowAny)
            {
                memoryCache.Set(req.ToString(), releases.FirstOrDefault(), CacheEntryOptions);
            }
            else
            {
                memoryCache.Set<Release?>(req.ToString(), null, CacheEntryOptions);
            }

            logger.LogDebug("No release with updater instructions found");
            await Send.NotFoundAsync(ct);
            return;
        }

        releaseWithInfo.EnsureUpdaterInstructions()?.EnsureFileContent();

        memoryCache.Set(req.ToString(), releaseWithInfo, CacheEntryOptions);

        if (asJson)
        {
            await Send.OkAsync(releaseWithInfo, ct);
            return;
        }

        UpdaterInstructionsFile? instructions = releaseWithInfo.UpdaterInstructions;

        if (instructions is null)
        {
            logger.LogDebug("Selected release has no updater instructions");
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.StringAsync(instructions.ToString(), cancellation: ct);
    }
}