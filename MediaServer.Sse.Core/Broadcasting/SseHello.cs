using System.Text.Json;
using MediaServer.Sse.Core.Models;

namespace MediaServer.Sse.Core.Broadcasting;

public static class SseHello
{
    public static string BuildFrame(string? version, string server)
    {
        var evt = new SseEvent
        {
            EventType = "hello",
            Version = version ?? "0.0.0.0",
            Server = server
        };
        return $"event: hello\ndata: {JsonSerializer.Serialize(evt)}\n\n";
    }
}
