using System;
using System.Linq;
using Xunit;

namespace Emby.Plugin.Sse.Tests;

public class PluginReferencesTests
{
    // Emby loads plugins against its own copies of these assemblies and fails if the plugin
    // asks for a newer version. Versions are what Emby 4.10's macOS build bundles (.NET 6).
    [Theory]
    [InlineData("System.Threading.Channels", "6.0.0.0")]
    [InlineData("System.Text.Json", "8.0.0.0")]
    [InlineData("Microsoft.Extensions.Logging.Abstractions", "8.0.0.0")]
    [InlineData("System.IO.Pipelines", "8.0.0.0")]
    public void References_NoNewerThanEmbyShips(string assemblyName, string maxVersion)
    {
        var reference = typeof(SsePlugin).Assembly.GetReferencedAssemblies()
            .SingleOrDefault(r => r.Name == assemblyName);

        if (reference is not null)
        {
            Assert.True(reference.Version <= Version.Parse(maxVersion),
                $"{assemblyName} {reference.Version} is newer than the {maxVersion} Emby ships");
        }
    }
}
