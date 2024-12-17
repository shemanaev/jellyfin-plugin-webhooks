using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Webhooks.Formats
{
    internal class DefaultFormat : IFormat
    {
        private readonly HttpClient _http;
        private readonly IDtoService _dto;
        private readonly IUserManager _users;

        public DefaultFormat(HttpClient http, IDtoService dto, IUserManager users)
        {
            _http = http;
            _dto = dto;
            _users = users;
        }

        public async Task Format(Uri url, EventInfo info)
        {
            var series = info.Item != null && info.Item is Episode ? _dto.GetBaseItemDto((info.Item as Episode).Series, new DtoOptions(true), info.User) : null;
            var item = info.Item == null ? null : _dto.GetBaseItemDto(info.Item, new DtoOptions(true), info.User);
            var user = info.User == null ? null : _users.GetUserDto(info.User);
            var body = new DefaultFormatPayload
            {
                Event = info.Event,
                Item = item,
                Session = info.Session,
                User = user,
                Server = info.Server,
                AdditionalData = info.AdditionalData,
                Series = series,
            };

            var content = new StringContent(JsonSerializer.Serialize(body, JsonDefaults.Options), Encoding.UTF8, "application/json");
            await _http.PostAsync(url, content);
        }
    }

    public class DefaultFormatPayload
    {
        public HookEvent Event { get; set; }
        public BaseItemDto Item { get; set; }
        public UserDto User { get; set; }
        public Dto.SessionInfoDto Session { get; set; }
        public ServerInfoDto Server { get; set; }
        public object AdditionalData { get; set; }
        public BaseItemDto Series { get; set; }
    }
}
