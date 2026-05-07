using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Sse
{
    public class SsePlugin : BasePlugin<BasePluginConfiguration>
    {
        public SsePlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
        }

        public override Guid Id => new Guid("A3D8F1E6-2B7C-4E9A-8F5D-1C6B0A3E7F92");

        public override string Name => "Tracearr SSE";

        public override string Description => "Server-Sent Events endpoint for real-time playback and session notifications.";
    }
}
