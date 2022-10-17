using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

using AdvancedUpdaterGitHubProxy.Extensions;

using FastEndpoints.Swagger;

using FluentValidation.Results;

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

ForwardedHeadersOptions headerOptions = new()
{
    ForwardedHeaders = ForwardedHeaders.All, RequireHeaderSymmetry = false, ForwardLimit = null
};

foreach (var proxy in GetNetworks(NetworkInterfaceType.Ethernet))
{
    headerOptions.KnownNetworks.Add(proxy);
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
    options.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Errors.StatusCode = StatusCodes.Status422UnprocessableEntity;
    options.Errors.ResponseBuilder = (list, context, arg3) => list.ToResponse();
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
    private static IEnumerable<IPNetwork> GetNetworks(NetworkInterfaceType type)
    {

        foreach (var item in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(n => n.NetworkInterfaceType == type && n.OperationalStatus == OperationalStatus.Up)  // get all operational networks of a given type
                     .Select(n => n.GetIPProperties())   // get the IPs
                     .Where(n => n.GatewayAddresses.Any())) // where the IPs have a gateway defined
        {
            var ipInfo = item.UnicastAddresses.FirstOrDefault(i => i.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); // get the first cluster-facing IP address
            if (ipInfo == null) { continue; }

            // convert the mask to bits
            var maskBytes = ipInfo.IPv4Mask.GetAddressBytes();
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(maskBytes);
            }
            var maskBits = new BitArray(maskBytes);

            // count the number of "true" bits to get the CIDR mask
            var cidrMask = maskBits.Cast<bool>().Count(b => b); 

            // convert my application's ip address to bits
            var ipBytes = ipInfo.Address.GetAddressBytes();
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(maskBytes);
            }
            var ipBits = new BitArray(ipBytes);

            // and the bits with the mask to get the start of the range
            var maskedBits = ipBits.And(maskBits);

            // Convert the masked IP back into an IP address
            var maskedIpBytes = new byte[4];
            maskedBits.CopyTo(maskedIpBytes, 0);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(maskedIpBytes);
            }
            var rangeStartIp = new IPAddress(maskedIpBytes);

            // return the start IP and CIDR mask
            yield return new IPNetwork(rangeStartIp, cidrMask);
        }
    }
}