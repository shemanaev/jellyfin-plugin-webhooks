using System.Threading.Tasks;
using Jellyfin.Data.Events;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Authentication;

namespace Jellyfin.Webhooks.Notifiers;

public class AuthenticationSuccessNotifier(
    IServerApplicationHost applicationHost,
    IUserManager userManager,
    ISender sender
) : IEventConsumer<GenericEventArgs<AuthenticationResult>>
{
    public async Task OnEvent(GenericEventArgs<AuthenticationResult> eventArgs)
    {
        if (eventArgs.Argument is null)
        {
            return;
        }

        var user = userManager.GetUserById(eventArgs.Argument.User.Id);
        await sender.Send(new EventInfo
        {
            Event = HookEvent.AuthenticationSucceeded,
            Session = new SessionInfoDto(eventArgs.Argument.SessionInfo),
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
