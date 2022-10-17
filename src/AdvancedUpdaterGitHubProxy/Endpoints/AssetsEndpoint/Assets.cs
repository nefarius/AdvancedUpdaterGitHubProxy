using AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

namespace AdvancedUpdaterGitHubProxy.Endpoints.AssetsEndpoint;

public class AssetsRequest
{
    /// <summary>
    ///     The GitHub user or organization name.
    /// </summary>
    [QueryParam]
    public string Username { get; set; } = default!;

    /// <summary>
    ///     The GitHub repository name.
    /// </summary>
    [QueryParam]
    public string Repository { get; set; } = default!;

    /// <summary>
    ///     Optional architecture. Valid values include: x86, x64 and arm64.
    /// </summary>
    [QueryParam]
    public string Architecture { get; set; } = default!;

    /// <summary>
    ///     Optional asset name. The first found asset is returned, if omitted.
    /// </summary>
    [QueryParam]
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
    }

    public override async Task HandleAsync(AssetsRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient? client = _httpClientFactory.CreateClient("GitHub");

        List<Release>? response = await client.GetFromJsonAsync<List<Release>>(
            $"https://api.github.com/repos/{req.Username}/{req.Repository}/releases",
            ct
        );

        if (response is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        IOrderedEnumerable<Release> releases = response.OrderByDescending(release => release.CreatedAt);

        Release? release = releases.FirstOrDefault();

        if (release is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        Asset? asset = string.IsNullOrEmpty(req.Architecture)
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