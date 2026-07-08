using MediaServer.Sse.Core.Broadcasting;
using Xunit;

namespace MediaServer.Sse.Tests.Broadcasting;

public class SseHelloTests
{
    [Fact]
    public void BuildFrame_ProducesNamedHelloEventWithVersionAndServer()
    {
        var frame = SseHello.BuildFrame("0.2.0.0", "jellyfin");
        Assert.Equal("event: hello\ndata: {\"version\":\"0.2.0.0\",\"server\":\"jellyfin\"}\n\n", frame);
    }

    [Fact]
    public void BuildFrame_UsesZeroVersion_WhenVersionUnknown()
    {
        var frame = SseHello.BuildFrame(null, "emby");
        Assert.Equal("event: hello\ndata: {\"version\":\"0.0.0.0\",\"server\":\"emby\"}\n\n", frame);
    }
}
