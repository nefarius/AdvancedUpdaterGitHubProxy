using AdvancedUpdaterGitHubProxy.Models;
using AdvancedUpdaterGitHubProxy.Services;

using Octokit;

using Release = Octokit.Release;

namespace AdvancedUpdaterGitHubProxy.Endpoints.AssetsEndpoint;

internal class AssetsEndpoint(
    IHttpClientFactory httpClientFactory,
    ILogger<AssetsEndpoint> logger,
    GitHubApiService github)
    : Endpoint<AssetsRequest>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes(
            "/api/github/{Username}/{Repository}/assets/latest",
            "/api/github/{Username}/{Repository}/assets/latest/{Architecture}",
            "/api/github/{Username}/{Repository}/assets/latest/{Architecture}/{Filename}"
        );
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Returns an asset from a GitHub release";
            s.Description =
                "Contacts the GitHub API, fetches the latest public release and returns the first found asset, if any.";
            s.Responses[200] = "The asset was returned successfully.";
            s.Responses[404] = "No public release was found.";
        });
    }

    public override async Task HandleAsync(AssetsRequest req, CancellationToken ct)
    {
        logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient client = httpClientFactory.CreateClient("GitHub");

        IReadOnlyList<Release>? response = await github.GetReleases(req.Username, req.Repository);

        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        IOrderedEnumerable<Release> releases = response.OrderByDescending(release => release.CreatedAt);

        Release? release = releases.FirstOrDefault();

        if (release is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        ReleaseAsset? asset = string.IsNullOrEmpty(req.Architecture)
            ? release.Assets.FirstOrDefault()
            : release.Assets.FirstOrDefault(a => a.Name.Contains(req.Architecture, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        using HttpResponseMessage resp = await client.GetAsync(asset.BrowserDownloadUrl, ct);

        resp.EnsureSuccessStatusCode();

        Stream stream = await resp.Content.ReadAsStreamAsync(ct);

        await Send.StreamAsync(
            stream,
            string.IsNullOrEmpty(req.Filename)
                ? asset.Name
                : req.Filename,
            asset.Size,
            asset.ContentType,
            cancellation: ct
        );
    }
}