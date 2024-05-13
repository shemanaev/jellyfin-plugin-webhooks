using Jellyfin.Data.Entities;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Webhooks;

public class EventInfo
{
    public HookEvent Event { get; set; }
    public User User { get; set; }
    public BaseItem Item { get; set; }
    public SessionInfoDto Session { get; set; }
    public ServerInfoDto Server { get; set; }
    public object AdditionalData { get; set; }
}
