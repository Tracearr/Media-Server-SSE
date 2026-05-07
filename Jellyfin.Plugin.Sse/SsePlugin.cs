using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Sse;

public class SsePlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
    : BasePlugin<BasePluginConfiguration>(applicationPaths, xmlSerializer)
{
    public override Guid Id => new("B4A6D7E2-8F3C-4A1E-9D5B-2C7F0E8A1B3D");

    public override string Name => "Tracearr SSE";

    public override string Description => "Server-Sent Events endpoint for real-time playback and session notifications.";
}
