namespace AdvancedUpdaterGitHubProxy.Endpoints.IndexEndpoint;

public class IndexEndpoint : EndpointWithoutRequest<IndexResponse>
{
    public override void Configure()
    {
        Get("/");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await SendOkAsync(new IndexResponse
        {
            Message = "Server up and running"
        }, ct);
    }
}
