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
using Microsoft.Extensions.Logging;

namespace Jellyfin.Webhooks.Formats
{
    internal class DefaultFormat : IFormat
    {
        private readonly HttpClient _http;
        private readonly IDtoService _dto;
        private readonly IUserManager _users;
        private readonly ILogger _logger;

        public DefaultFormat(HttpClient http, IDtoService dto, IUserManager users, ILogger logger)
        {
            _http = http;
            _dto = dto;
            _users = users;
            _logger = logger;
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

            var contentJson = JsonSerializer.Serialize(body, JsonDefaults.Options);
            var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
            _logger.LogInformation("Calling url: {url} (size: {size})", url, contentJson.Length);
            var response = await _http.PostAsync(url, content);
            _logger.LogInformation($"Response: {response}");
        }
    }

    public class DefaultFormatPayload
    {
        public HookEvent Event { get; set; }
        public BaseItemDto Item { get; set; }
        public UserDto User { get; set; }
        public SessionInfoDto Session { get; set; }
        public ServerInfoDto Server { get; set; }
        public object AdditionalData { get; set; }
        public BaseItemDto Series { get; set; }
    }
}
