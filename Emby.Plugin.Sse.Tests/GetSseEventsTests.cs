using System.Reflection;
using Emby.Plugin.Sse.Api;
using MediaBrowser.Controller.Net;
using Xunit;

namespace Emby.Plugin.Sse.Tests;

public class GetSseEventsTests
{
    [Fact]
    public void EventsRequireAnAdministrator()
    {
        var attribute = typeof(GetSseEvents).GetCustomAttribute<AuthenticatedAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute.Roles);
    }
}
