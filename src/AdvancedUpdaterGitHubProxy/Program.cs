using System.Net;
using System.Text.Json;

using AdvancedUpdaterGitHubProxy.Extensions;

using FastEndpoints.Swagger;

using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

//using Prometheus;

using Serilog;
using Serilog.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

#region Configuration

IConfigurationRoot? configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .Build();

#endregion

#region Logging

Logger? logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

// logger instance used by non-DI-code
Log.Logger = logger;

builder.Host.UseSerilog(logger);

builder.Services.AddLogging(b =>
{
    b.SetMinimumLevel(LogLevel.Information);
    b.AddSerilog(logger, true);
});

builder.Services.AddSingleton(new LoggerFactory().AddSerilog(logger));

builder.Services.AddW3CLogging(logging =>
{
    // Log all W3C fields
    logging.LoggingFields = W3CLoggingFields.All;

    logging.FileSizeLimit = 5 * 1024 * 1024;
    logging.RetainedFileCountLimit = 30;
    logging.FileName = "access-";
    logging.LogDirectory = @"logs";
    logging.FlushInterval = TimeSpan.FromSeconds(2);
});

#endregion

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.AllowSynchronousIO = false;
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddFastEndpoints(options =>
{
    options.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All;
});

builder.Services.AddSwaggerDoc(addJWTBearerAuth: false);

builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDefaultExceptionHandler();
}

List<IPAddress> knownProxies =
    configuration.GetSection("KnownProxies").Get<List<string>>().Select(IPAddress.Parse).ToList();

ForwardedHeadersOptions headerOptions = new()
{
    ForwardedHeaders = ForwardedHeaders.All, RequireHeaderSymmetry = false, ForwardLimit = null
};

foreach (var proxy in knownProxies)
{
    headerOptions.KnownProxies.Add(proxy);
}

app.UseForwardedHeaders(headerOptions);

app.UseSerilogRequestLogging(
    options =>
    {
        options.MessageTemplate =
            "{RemoteIpAddress} {RequestScheme} {RequestHost} {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (
            diagnosticContext,
            httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
        };
    });

app.UseW3CLogging();

//app.UseMetricServer();
//app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(options =>
{
    options.SerializerOptions = x => x.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.ErrorResponseStatusCode = StatusCodes.Status422UnprocessableEntity;
    options.ErrorResponseBuilder = (failures, _) => failures.ToResponse();
});

app.UseDefaultFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"wwwroot"))
});

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi3(x => x.ConfigureDefaults());
}

await app.RunAsync();

public partial class Program
{
}