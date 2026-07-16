using System.Net.Mime;

namespace CMS.API.Infrastructure;

/// <summary>
/// Last-resort handler for exceptions that escape a controller or repository. It logs the full
/// exception server-side and returns ONE consistent 500 body carrying only a generic, safe message —
/// never the exception message, stack trace, SQL text or connection details.
///
/// It only reacts to <b>thrown</b> exceptions. Deliberate responses — 401 (unauthenticated), 403
/// (forbidden) and the [ApiController] 400 validation problem — are normal responses, not exceptions,
/// so they flow straight through untouched. The generic error body a client receives:
/// <c>{ "message": "An unexpected error occurred." }</c>.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    /// <summary>The only detail an unexpected 500 ever exposes to the client.</summary>
    public const string GenericMessage = "An unexpected error occurred.";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Full detail stays on the server log; nothing here reaches the client.
            logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // The response is already on the wire — we can no longer replace it, so let it fail.
            if (context.Response.HasStarted)
            {
                throw;
            }

            // Don't clear the whole response: that would drop the CORS headers an inner middleware
            // added, and the browser could not then read this body. Nothing was written yet
            // (HasStarted is false), so setting status + a fresh JSON body is safe.
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(new { message = GenericMessage }, context.RequestAborted);
        }
    }
}
