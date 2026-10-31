using System.Reflection;
using SmartFade.Api.Contracts;
using SmartFade.Api.Hubs;
using SmartFade.Api.Realtime;
using SmartFade.Api.Security;
using SmartFade.Application;
using SmartFade.Devices.Simulated;
using SmartFade.Devices.Usb;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

var backendVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0.0.0";

// Device selection. The simulator is the default. "Usb" is the real console, read-only, behind
// safety check SC-1 (docs/protocol/safety-checks.md): it must be asked for explicitly, for this
// run, and any other value refuses to start rather than silently falling back.
var deviceMode = builder.Configuration["Device:Mode"] ?? "Simulated";
var usbMode = string.Equals(deviceMode, "Usb", StringComparison.OrdinalIgnoreCase);
if (!usbMode && !string.Equals(deviceMode, "Simulated", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Device:Mode '{deviceMode}' is not available. Use 'Simulated' (default) or 'Usb' (the real console, read-only, safety check SC-1).");
}

var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "http://localhost:5160", "http://127.0.0.1:5160" };

if (usbMode)
{
    // "This run only" (SC-1 item 6): the mode must be confirmed for today's date, so a setting left
    // in a shell, a launch profile or appsettings cannot switch a later run to the console by accident.
    var confirmDate = builder.Configuration["Device:Usb:ConfirmDate"];
    if (!SmartFade.Devices.Usb.ServiceCollectionExtensions.IsConfirmedForToday(confirmDate, DateOnly.FromDateTime(DateTime.Now)))
    {
        throw new InvalidOperationException(
            $"Device:Mode=Usb needs Device:Usb:ConfirmDate={DateTime.Now:yyyy-MM-dd} (today) on the same command line: hardware mode is confirmed per day, never left switched on.");
    }

    builder.Services.AddUsbSmartFadeDevice(builder.Configuration, backendVersion);
}
else
{
    builder.Services.AddSimulatedSmartFadeDevice(builder.Configuration);
}

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

// The show-file page: a .asc file chosen in the browser is parsed by SmartFade.ShowFile.Asc and returned
// as a DTO. Read-only: nothing is stored and nothing reaches a console. The body is the file itself.
app.MapPost("/api/showfile/parse", async (HttpRequest request, string? name, CancellationToken cancellationToken) =>
{
    const int maxBytes = 4 * 1024 * 1024;   // a show file is about 20 KB; this is a sanity limit, not a format one
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, cancellationToken);
    if (buffer.Length == 0)
    {
        return Results.BadRequest(new ErrorDto("The request body must be the .asc file.", "invalid_argument"));
    }

    if (buffer.Length > maxBytes)
    {
        return Results.BadRequest(new ErrorDto("The file is larger than a show file can be.", "invalid_argument"));
    }

    var bytes = buffer.ToArray();
    var read = SmartFade.ShowFile.Asc.AscReader.Read(bytes);
    return Results.Ok(ShowFileMapper.ToDto(string.IsNullOrWhiteSpace(name) ? "show.asc" : name.Trim(), bytes.Length, read));
});

// The editing pages: the UI sends the DTO it edited and receives the rendered .asc bytes to save as a NEW file.
// The backend keeps nothing and overwrites nothing; the patch is checked against the 1248's ranges first.
app.MapPost("/api/showfile/render", (ShowFileDto dto) =>
{
    var problem = ShowFileRules.Check(dto);
    if (problem is not null)
    {
        return Results.BadRequest(new ErrorDto(problem, "invalid_argument"));
    }

    try
    {
        var bytes = SmartFade.ShowFile.Asc.AscWriter.Render(ShowFileMapper.ToModel(dto));
        return Results.File(bytes, "application/octet-stream", ShowFileRules.EditedFileName(dto.Name));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorDto(ex.Message, "invalid_argument"));
    }
});

app.MapHub<ConsoleHub>("/hubs/console");

if (usbMode)
{
    app.Logger.LogWarning("HARDWARE MODE (read-only): the backend will talk to the real console when Connect is pressed; only the poll and the listed fetch are sent (SC-1). Back up the show first.");
}

app.Logger.LogInformation("SmartFade backend {Version} starting in {Mode} mode", backendVersion, usbMode ? "Usb (hardware, read-only)" : "Simulated");
app.Run();

/// <summary>Makes the entry point visible to WebApplicationFactory in the API tests.</summary>
public partial class Program;
