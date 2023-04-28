using System.Diagnostics.CodeAnalysis;

using AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

namespace AdvancedUpdaterGitHubProxy.Endpoints.AssetsEndpoint;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class AssetsRequest
{
    /// <summary>
    ///     The GitHub user or organization name.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>
    ///     The GitHub repository name.
    /// </summary>
    public string Repository { get; set; } = default!;

    /// <summary>
    ///     Optional architecture. Valid values include: x86, x64 and arm64.
    /// </summary>
    public string Architecture { get; set; } = default!;

    /// <summary>
    ///     Optional filename the response should use. Some clients can't handle the URL not ending with a "real" file name, so
    ///     this value will be reflected in the response.
    /// </summary>
    public string Filename { get; set; } = default!;

    public override string ToString()
    {
        return $"{Username}/{Repository}";
    }
}

public class AssetsEndpoint : Endpoint<AssetsRequest>
{
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ILogger<AssetsEndpoint> _logger;

    public AssetsEndpoint(IHttpClientFactory httpClientFactory, ILogger<AssetsEndpoint> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

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
        _logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient client = _httpClientFactory.CreateClient("GitHub");

        List<Release> response = await client.GetFromJsonAsync<List<Release>>(
            $"https://api.github.com/repos/{req.Username}/{req.Repository}/releases",
            ct
        );

        if (response is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        IOrderedEnumerable<Release> releases = response.OrderByDescending(release => release.CreatedAt);

        Release release = releases.FirstOrDefault();

        if (release is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        Asset asset = string.IsNullOrEmpty(req.Architecture)
            ? release.Assets.FirstOrDefault()
            : release.Assets.FirstOrDefault(a => a.Name.Contains(req.Architecture, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        using HttpResponseMessage resp = await client.GetAsync(asset.BrowserDownloadUrl, ct);

        resp.EnsureSuccessStatusCode();

        Stream stream = await resp.Content.ReadAsStreamAsync(ct);

        await SendStreamAsync(
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