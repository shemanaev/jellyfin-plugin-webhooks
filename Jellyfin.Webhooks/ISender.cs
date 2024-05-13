using System.Threading.Tasks;

namespace Jellyfin.Webhooks;

public interface ISender
{
    Task Send(EventInfo request);
}
