using System;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Webhooks.Formats
{
    internal class DefaultFormat : IFormat
    {
        private readonly IJsonSerializer _json;
        private readonly IHttpClient _http;
        private readonly IDtoService _dto;
        private readonly IUserManager _users;

        public DefaultFormat(IJsonSerializer json, IHttpClient http, IDtoService dto, IUserManager users)
        {
            _json = json;
            _http = http;
            _dto = dto;
            _users = users;
        }

        public async Task Format(Uri url, EventInfo info)
        {
            var item = _dto.GetBaseItemDto(info.Item, new DtoOptions(true), info.User);
            var user = _users.GetUserDto(info.User);
            var body = new DefaultFormatPayload
            {
                Event = info.Event,
                Item = item,
                Session = info.Session,
                User = user,
                Server = info.Server
            };

            var content = _json.SerializeToString(body);
            var options = new HttpRequestOptions
            {
                RequestContentType = "application/json",
                LogErrorResponseBody = true,
                EnableDefaultUserAgent = true,
                Url = url.ToString(),
                RequestContent = content,
            };
            await _http.SendAsync(options, HttpMethod.Post);
        }
    }

    public class DefaultFormatPayload
    {
        public HookEvent Event { get; set; }
        public BaseItemDto Item { get; set; }
        public UserDto User { get; set; }
        public SessionInfoDto Session { get; set; }
        public ServerInfoDto Server { get; set; }
    }
}
