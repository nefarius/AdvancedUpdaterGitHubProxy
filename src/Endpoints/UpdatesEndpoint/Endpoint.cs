using System.Net;

using AdvancedUpdaterGitHubProxy.Models;
using AdvancedUpdaterGitHubProxy.Services;

using Microsoft.Extensions.Caching.Memory;

using Octokit;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

internal class UpdatesEndpoint(
    IMemoryCache memoryCache,
    ILogger<UpdatesEndpoint> logger,
    IConfiguration config,
    GitHubApiService github)
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

        UpdatesEndpointConfig? epConfig = config.GetSection("UpdatesEndpoint").Get<UpdatesEndpointConfig>();

        if ((epConfig is not null && epConfig.BlacklistedUsernames.Contains(req.Username)) ||
            (epConfig is not null && epConfig.BlacklistedRepositories.Contains(req.Repository)))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        IPAddress? remoteIpAddress = HttpContext.Request.HttpContext.Connection.RemoteIpAddress;

        // check for beta client
        bool isBetaClient = epConfig?.BetaClients is not null &&
                            remoteIpAddress is not null &&
                            epConfig.BetaClients.Contains(remoteIpAddress);

        switch (isBetaClient)
        {
            case true:
                logger.LogWarning("Client {Remote} is beta client, bypassing cache and delivering pre-releases",
                    remoteIpAddress);
                break;
            // never deliver cached result to beta clients
            case false when memoryCache.TryGetValue(req.ToString(), out UpdateRelease? cached):
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

        IReadOnlyList<Release>? releases = await github.GetReleases(req.Username, req.Repository);

        if (releases is null)
        {
            logger.LogDebug("No releases returned from GitHub API");
            await Send.NotFoundAsync(ct);
            return;
        }

        List<UpdateRelease> releasesSorted = releases
            .OrderByDescending(release => release.CreatedAt)
            .Select(r => new UpdateRelease(r))
            .ToList();

        UpdateRelease? releaseWithInfo = isBetaClient
            ? releasesSorted.FirstOrDefault(r => allowAny || r.EnsureUpdaterInstructions() is not null)
            : releasesSorted.FirstOrDefault(r =>
                allowAny || (!r.Prerelease && r.EnsureUpdaterInstructions() is not null));

        if (releaseWithInfo is null)
        {
            if (asJson || allowAny)
            {
                memoryCache.Set(req.ToString(), releasesSorted.FirstOrDefault(), CacheEntryOptions);
            }
            else
            {
                memoryCache.Set<UpdateRelease?>(req.ToString(), null, CacheEntryOptions);
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