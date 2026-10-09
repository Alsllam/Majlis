using Microsoft.AspNetCore.ResponseCompression;
using Serilog;

// The BFF is the only internet-facing host: YARP routes, security headers and compression. No business logic.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(self)";
    headers.ContentSecurityPolicy = "frame-ancestors 'none'";
    await next();
});
app.UseResponseCompression();
app.UseWebSockets();
app.MapHealthChecks("/health");
app.MapReverseProxy();
app.Run();
