using System.Threading.Tasks;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using MediaBrowser.Controller.Events.Authentication;

namespace Jellyfin.Webhooks.Notifiers;

public class AuthenticationFailureNotifier(
    IServerApplicationHost applicationHost,
    ISender sender
) : IEventConsumer<AuthenticationRequestEventArgs>
{
    public async Task OnEvent(AuthenticationRequestEventArgs eventArgs)
    {
        if (eventArgs is null)
        {
            return;
        }

        await sender.Send(new EventInfo
        {
            Event = HookEvent.AuthenticationFailed,
            AdditionalData = eventArgs,
            Server = new ServerInfoDto
            {
                Id = applicationHost.SystemId,
                Name = applicationHost.FriendlyName,
                Version = applicationHost.ApplicationVersion.ToString(),
            },
        });
    }
}
