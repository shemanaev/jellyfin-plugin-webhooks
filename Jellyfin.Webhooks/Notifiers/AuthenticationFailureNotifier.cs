using System.Threading.Tasks;
using Jellyfin.Data.Events;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;

namespace Jellyfin.Webhooks.Notifiers;

public class AuthenticationFailureNotifier(
    IServerApplicationHost applicationHost,
    ISender sender
) : IEventConsumer<GenericEventArgs<AuthenticationRequest>>
{
    public async Task OnEvent(GenericEventArgs<AuthenticationRequest> eventArgs)
    {
        if (eventArgs.Argument is null)
        {
            return;
        }

        await sender.Send(new EventInfo
        {
            Event = HookEvent.AuthenticationFailed,
            AdditionalData = eventArgs.Argument,
            Server = new ServerInfoDto
            {
                Id = applicationHost.SystemId,
                Name = applicationHost.FriendlyName,
                Version = applicationHost.ApplicationVersion.ToString(),
            },
        });
    }
}
