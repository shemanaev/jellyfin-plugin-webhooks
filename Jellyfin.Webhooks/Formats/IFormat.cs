using System;
using System.Threading.Tasks;

namespace Jellyfin.Webhooks.Formats
{
    internal interface IFormat
    {
        Task Format(Uri url, EventInfo info);
    }
}
