using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.Sse.Api
{
    [Route("/sse/events", "GET")]
    [Authenticated(Roles = "Admin")]
    public class GetSseEvents : IReturn<object>
    {
    }
}
