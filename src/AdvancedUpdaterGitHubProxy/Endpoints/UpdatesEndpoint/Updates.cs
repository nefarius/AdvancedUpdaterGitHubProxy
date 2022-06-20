namespace AdvancedUpdaterGitHubProxy.Endpoints.UpdatesEndpoint;

public class UpdatesRequest
{
    public string Username { get; set; } = default!;

    public string Repository { get; set; } = default!;
}

public class UpdatesEndpoint : Endpoint<UpdatesRequest>
{
    private readonly IHttpClientFactory _httpClientFactory;

    public UpdatesEndpoint(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/api/github/{Username}/{Repository}/updates");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdatesRequest req, CancellationToken ct)
    {
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

        await SendStringAsync(instructions.ToString(), cancellation: ct);
    }
}