using System.Net.Http.Headers;
using System.Text.Json;

using AdvancedUpdaterGitHubProxy;
using AdvancedUpdaterGitHubProxy.Extensions;

using FastEndpoints.Swagger;

using Microsoft.Extensions.FileProviders;

using Nefarius.Utilities.AspNetCore;

using Polly;
using Polly.Contrib.WaitAndRetry;

using Prometheus;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args).Setup();

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.AllowSynchronousIO = false;
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddFastEndpoints(options =>
{
    options.SourceGeneratorDiscoveredTypes.AddRange(DiscoveredTypes.All);
});

builder.Services.SwaggerDocument();

builder.Services.AddHttpClient("GitHub", client =>
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AdvancedUpdaterGitHubProxy", "1"));
    })
    .AddTransientHttpErrorPolicy(pb =>
        pb.WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5)));

builder.Services.AddMemoryCache();

builder.Services.AddMetricServer(options =>
{
    options.Port = 1337;
});

WebApplication app = builder.Build().Setup();

if (app.Environment.IsDevelopment())
{
    app.UseDefaultExceptionHandler();
}

app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(options =>
{
    options.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Errors.StatusCode = StatusCodes.Status422UnprocessableEntity;
    options.Errors.ResponseBuilder = (list, context, arg3) => list.ToResponse();
});

app.MapMetrics();

if (app.Environment.IsProduction())
{
    string wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"wwwroot");

    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
}

app.UseOpenApi();
app.UseSwaggerUi3(x => x.ConfigureDefaults());

await app.RunAsync();