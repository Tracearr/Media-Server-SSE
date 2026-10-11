using System.Reflection;
using Jellyfin.Plugin.Sse.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace MediaServer.Sse.Tests.Api;

public class SseControllerTests
{
    [Fact]
    public void EventsRequireAnAdministrator()
    {
        var attribute = typeof(SseController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(Policies.RequiresElevation, attribute.Policy);
    }
}
