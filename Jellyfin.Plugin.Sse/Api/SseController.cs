using System.Text.Json;
using MediaServer.Sse.Core.Broadcasting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Sse.Api;

[Authorize]
[ApiController]
[Route("api/sse")]
public class SseController(ISseEventBroadcaster broadcaster, ILogger<SseController> logger) : ControllerBase
{
    [HttpGet("events")]
    [Produces("text/event-stream")]
    public async Task GetEvents()
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        var (id, reader) = broadcaster.Subscribe();
        logger.LogInformation("SSE client connected: {Id}", id);

        try
        {
            await foreach (var evt in reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                var data = JsonSerializer.Serialize(evt);
                await Response.WriteAsync($"event: {evt.EventType}\ndata: {data}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            broadcaster.Unsubscribe(id);
            logger.LogInformation("SSE client disconnected: {Id}", id);
        }
    }
}
