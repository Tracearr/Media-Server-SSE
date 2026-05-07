using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MediaBrowser.Model.Services;
using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;

namespace Emby.Plugin.Sse.Api
{
    public class SseStreamWriter : IAsyncStreamWriter
    {
        private static readonly byte[] EventPrefix = Encoding.UTF8.GetBytes("event: ");
        private static readonly byte[] DataPrefix = Encoding.UTF8.GetBytes("\ndata: ");
        private static readonly byte[] FrameTerminator = Encoding.UTF8.GetBytes("\n\n");

        private readonly ISseEventBroadcaster _broadcaster;

        public SseStreamWriter(ISseEventBroadcaster broadcaster)
        {
            _broadcaster = broadcaster;
        }

        public async Task WriteToAsync(IResponse response, CancellationToken cancellationToken)
        {
            response.ContentType = "text/event-stream";
            response.SendChunked = true;
            response.AddHeader("Cache-Control", "no-cache");
            response.AddHeader("X-Accel-Buffering", "no");

            var writer = response.OutputWriter;
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
