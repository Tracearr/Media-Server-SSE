using System;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.Sse.Api
{
    public class SseService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; } = null!;

        public object Get(GetSseEvents request)
        {
            var broadcaster = SseEntryPoint.Broadcaster;
            if (broadcaster == null)
            {
                throw new InvalidOperationException("SSE broadcaster not available");
            }

            var version = typeof(SsePlugin).Assembly.GetName().Version?.ToString();
            return new SseStreamWriter(broadcaster, version);
        }
    }
}
