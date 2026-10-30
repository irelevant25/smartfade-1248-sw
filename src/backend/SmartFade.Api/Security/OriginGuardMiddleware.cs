namespace SmartFade.Api.Security;

/// <summary>
/// Rejects browser requests whose Origin header is not one of the configured local origins,
/// before CORS, SignalR and the endpoints see them. CORS alone only hides responses; a cross-site
/// POST or a forged-Origin WebSocket upgrade would still execute. With the simulator this is
/// harmless; it is a precondition for any future hardware mode (no web page may ever drive a
/// console without the owner's action). Requests without an Origin header (same-origin
/// navigations, curl, tests) pass.
/// </summary>
public sealed class OriginGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedOrigins;
    private readonly ILogger<OriginGuardMiddleware> _logger;

    public OriginGuardMiddleware(RequestDelegate next, IEnumerable<string> allowedOrigins, ILogger<OriginGuardMiddleware> logger)
    {
        _next = next;
        _allowedOrigins = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !_allowedOrigins.Contains(origin))
        {
            _logger.LogWarning("Rejected request from origin {Origin} to {Path}", origin, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Origin not allowed.");
            return;
        }

        await _next(context);
    }
}
