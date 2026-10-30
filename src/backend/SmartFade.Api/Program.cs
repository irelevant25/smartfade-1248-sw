using System.Reflection;
using SmartFade.Api.Contracts;
using SmartFade.Api.Hubs;
using SmartFade.Api.Realtime;
using SmartFade.Api.Security;
using SmartFade.Application;
using SmartFade.Devices.Simulated;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// Device selection. Phase 1 knows the simulator only: no protocol message is cleared for
// sending (CLAUDE.md), so there is no hardware mode to configure, and any other value refuses
// to start rather than silently falling back.
var deviceMode = builder.Configuration["Device:Mode"] ?? "Simulated";
if (!string.Equals(deviceMode, "Simulated", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Device:Mode '{deviceMode}' is not available. Phase 1 supports 'Simulated' only: no protocol message is cleared for sending to a console.");
}

var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "http://localhost:5160", "http://127.0.0.1:5160" };

builder.Services.AddSimulatedSmartFadeDevice(builder.Configuration);
builder.Services.AddSmartFadeApplication();
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
builder.Services.AddHostedService<ConsoleEventsBroadcaster>();

var app = builder.Build();

app.UseMiddleware<OriginGuardMiddleware>((object)allowedOrigins.AsEnumerable());
app.UseCors();
if (Directory.Exists(app.Environment.WebRootPath))
{
    // the built Angular app, when it has been published into wwwroot
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

var backendVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0.0.0";

app.MapGet("/api/status", (ConsoleService console) =>
{
    var connection = DtoMapper.ToDto(console.GetState().Connection);
    return new StatusDto(connection.Mode, connection, backendVersion, new ProtocolKnowledgeDto(DtoMapper.MessagesClearedForSending));
});

app.MapGet("/api/console/state", (ConsoleService console) => DtoMapper.ToDto(console.GetState()));

app.MapPost("/api/console/connect", async (ConsoleService console, CancellationToken cancellationToken) =>
{
    await console.ConnectAsync(cancellationToken);
    return DtoMapper.ToDto(console.GetState().Connection);
});

app.MapPost("/api/console/disconnect", async (ConsoleService console, CancellationToken cancellationToken) =>
{
    await console.DisconnectAsync(cancellationToken);
    return DtoMapper.ToDto(console.GetState().Connection);
});

app.MapHub<ConsoleHub>("/hubs/console");

app.Logger.LogInformation("SmartFade backend {Version} starting in {Mode} mode", backendVersion, deviceMode);
app.Run();

/// <summary>Makes the entry point visible to WebApplicationFactory in the API tests.</summary>
public partial class Program;
