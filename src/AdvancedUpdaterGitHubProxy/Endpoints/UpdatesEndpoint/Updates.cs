using Microsoft.Extensions.Caching.Memory;

namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

public class UpdatesRequest
{
    public string Username { get; set; } = default!;

    public string Repository { get; set; } = default!;

    public override string ToString()
    {
        return $"{Username}/{Repository}";
    }
}

public class UpdatesEndpoint : Endpoint<UpdatesRequest>
{
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ILogger<UpdatesEndpoint> _logger;

    private readonly IMemoryCache _memoryCache;

    public UpdatesEndpoint(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache,
        ILogger<UpdatesEndpoint> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/api/github/{Username}/{Repository}/updates");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdatesRequest req, CancellationToken ct)
    {
        if (_memoryCache.TryGetValue(req.ToString(), out string cached))
        {
            _logger.LogInformation("Returning cached response for {Request}", req.ToString());

            await SendStringAsync(cached, cancellation: ct);
            return;
        }

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

        Release? release = releases.FirstOrDefault(r => r.UpdaterInstructions is not null);

        if (release is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        UpdaterInstructionsFile? instructions = release.UpdaterInstructions;

        if (instructions is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        MemoryCacheEntryOptions? cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromHours(1));

        _memoryCache.Set(req.ToString(), instructions.ToString(), cacheEntryOptions);

        await SendStringAsync(instructions.ToString(), cancellation: ct);
    }
}