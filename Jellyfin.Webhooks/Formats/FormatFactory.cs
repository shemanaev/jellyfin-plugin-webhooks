using Jellyfin.Webhooks.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Webhooks.Formats
{
    internal class FormatFactory
    {
        private readonly IJsonSerializer _json;
        private readonly IHttpClient _http;
        private readonly IDtoService _dto;
        private readonly IUserManager _users;

        public FormatFactory(IJsonSerializer json, IHttpClient http, IDtoService dto, IUserManager users)
        {
            _json = json;
            _http = http;
            _dto = dto;
            _users = users;
        }

        public IFormat CreateFormat(HookFormat format)
        {
            switch (format)
            {
                case HookFormat.Get:
                    return new GetFormat(_http);

                case HookFormat.Plex:
                    return new PlexFormat(_json);

                default:
                    return new DefaultFormat(_json, _http, _dto, _users);
            }
        }
    }
}
