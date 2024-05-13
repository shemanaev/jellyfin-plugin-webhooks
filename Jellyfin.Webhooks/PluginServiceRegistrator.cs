using Jellyfin.Data.Events;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Webhooks.Notifiers;

namespace Jellyfin.Webhooks;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHostedService<EntryPoint>();
        serviceCollection.AddScoped<ISender, Sender>();

        // Security consumers.
        serviceCollection.AddScoped<IEventConsumer<GenericEventArgs<AuthenticationRequest>>, AuthenticationFailureNotifier>();
        serviceCollection.AddScoped<IEventConsumer<GenericEventArgs<AuthenticationResult>>, AuthenticationSuccessNotifier>();
    }
}
