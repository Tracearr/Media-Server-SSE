using MediaServer.Sse.Core.Broadcasting;
using Jellyfin.Plugin.Sse.Consumers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.Sse;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ISseEventBroadcaster, SseEventBroadcaster>();

        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartSseConsumer>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopSseConsumer>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackProgressEventArgs>, PlaybackProgressSseConsumer>();
        serviceCollection.AddScoped<IEventConsumer<SessionStartedEventArgs>, SessionStartSseConsumer>();
        serviceCollection.AddScoped<IEventConsumer<SessionEndedEventArgs>, SessionEndSseConsumer>();

        serviceCollection.AddHostedService<LibrarySseConsumer>();
        serviceCollection.AddHostedService<TaskSseConsumer>();
        serviceCollection.AddHostedService<ServerStatsSseService>();
    }
}
