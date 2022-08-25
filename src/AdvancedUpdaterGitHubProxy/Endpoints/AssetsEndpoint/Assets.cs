using AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

namespace AdvancedUpdaterGitHubProxy.Endpoints.AssetsEndpoint;

public class AssetsRequest
{
    public string Username { get; set; } = default!;

    public string Repository { get; set; } = default!;

    public string Architecture { get; set; } = default!;

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
        Routes("/api/github/{Username}/{Repository}/assets/latest",
            "/api/github/{Username}/{Repository}/assets/latest/{Architecture}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssetsRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Contacting GitHub API for {Request}", req.ToString());

        using HttpClient? client = _httpClientFactory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 6.0) " +
            "(compatible; MSIE 6.0; Windows NT 5.1; " +
            ".NET CLR 1.1.4322; .NET CLR 2.0.50727)"
        );

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

        await SendStreamAsync(stream, asset.Name, asset.Size, asset.ContentType, cancellation: ct);
    }
}