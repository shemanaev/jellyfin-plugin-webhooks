using System.Threading.Tasks;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Events.Authentication;

namespace Jellyfin.Webhooks.Notifiers;

public class AuthenticationSuccessNotifier(
    IServerApplicationHost applicationHost,
    IUserManager userManager,
    ISender sender
) : IEventConsumer<AuthenticationResultEventArgs>
{
    public async Task OnEvent(AuthenticationResultEventArgs eventArgs)
    {
        if (eventArgs is null)
        {
            return;
        }

        var user = userManager.GetUserById(eventArgs.User.Id);
        await sender.Send(new EventInfo
        {
            Event = HookEvent.AuthenticationSucceeded,
            Session = new SessionInfoDto(eventArgs.SessionInfo),
            User = user,
            Server = new ServerInfoDto
            {
                Id = applicationHost.SystemId,
                Name = applicationHost.FriendlyName,
                Version = applicationHost.ApplicationVersion.ToString(),
            },
        });
    }
}
