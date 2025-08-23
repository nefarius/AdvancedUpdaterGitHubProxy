using System.Net.Http.Headers;
using System.Text.Json;

using AdvancedUpdaterGitHubProxy;
using AdvancedUpdaterGitHubProxy.Extensions;

using FastEndpoints.Swagger;

using Microsoft.Extensions.FileProviders;

using Nefarius.Utilities.AspNetCore;

using Polly;
using Polly.Contrib.WaitAndRetry;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args).Setup();

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", builder.Configuration["GitHub:Token"]);
    })
    .AddTransientHttpErrorPolicy(pb => pb
        //.OrResult(message => message.StatusCode == HttpStatusCode.Forbidden)
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(5), 5)));

builder.Services.AddMemoryCache();


WebApplication app = builder.Build().Setup();

if (app.Environment.IsDevelopment())
{
    app.UseDefaultExceptionHandler();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(options =>
{
    options.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Errors.StatusCode = StatusCodes.Status422UnprocessableEntity;
    options.Errors.ResponseBuilder = (list, context, arg3) => list.ToResponse();
}).UseSwaggerGen();

if (app.Environment.IsProduction())
{
    string wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"wwwroot");

    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
}

await app.RunAsync();