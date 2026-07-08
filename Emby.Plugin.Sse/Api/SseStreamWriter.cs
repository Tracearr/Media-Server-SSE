using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediaBrowser.Model.Services;
using MediaServer.Sse.Core.Broadcasting;

namespace Emby.Plugin.Sse.Api
{
    public class SseStreamWriter : IAsyncStreamWriter
    {
        private static readonly byte[] EventPrefix = Encoding.UTF8.GetBytes("event: ");
        private static readonly byte[] DataPrefix = Encoding.UTF8.GetBytes("\ndata: ");
        private static readonly byte[] FrameTerminator = Encoding.UTF8.GetBytes("\n\n");

        private readonly ISseEventBroadcaster _broadcaster;
        private readonly string? _version;

        public SseStreamWriter(ISseEventBroadcaster broadcaster, string? version)
        {
            _broadcaster = broadcaster;
            _version = version;
        }

        public async Task WriteToAsync(IResponse response, CancellationToken cancellationToken)
        {
            response.ContentType = "text/event-stream";
            response.SendChunked = true;
            response.AddHeader("Cache-Control", "no-cache");
            response.AddHeader("X-Accel-Buffering", "no");

            var writer = response.OutputWriter;

            var helloBytes = Encoding.UTF8.GetBytes(SseHello.BuildFrame(_version, "emby"));
            var helloMemory = writer.GetMemory(helloBytes.Length);
            helloBytes.AsMemory().CopyTo(helloMemory);
            writer.Advance(helloBytes.Length);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            var subscription = _broadcaster.Subscribe();
            var id = subscription.Id;
            var reader = subscription.Reader;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    while (reader.TryRead(out var evt))
                    {
                        var eventTypeBytes = Encoding.UTF8.GetBytes(evt.EventType);
                        var dataBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

                        var totalLength = EventPrefix.Length + eventTypeBytes.Length
                            + DataPrefix.Length + dataBytes.Length
                            + FrameTerminator.Length;

                        var memory = writer.GetMemory(totalLength);
                        var offset = 0;

                        EventPrefix.AsMemory().CopyTo(memory.Slice(offset));
                        offset += EventPrefix.Length;
                        eventTypeBytes.AsMemory().CopyTo(memory.Slice(offset));
                        offset += eventTypeBytes.Length;
                        DataPrefix.AsMemory().CopyTo(memory.Slice(offset));
                        offset += DataPrefix.Length;
                        dataBytes.AsMemory().CopyTo(memory.Slice(offset));
                        offset += dataBytes.Length;
                        FrameTerminator.AsMemory().CopyTo(memory.Slice(offset));
                        offset += FrameTerminator.Length;

                        writer.Advance(offset);

                        var result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                        if (result.IsCompleted)
                        {
                            return;
                        }
                    }

                    if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected on client disconnect
            }
            finally
            {
                _broadcaster.Unsubscribe(id);
            }
        }
    }
}
