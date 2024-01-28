using System.Net.Http;
using Jellyfin.Webhooks.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Webhooks.Formats
{
    internal class FormatFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDtoService _dto;
        private readonly IUserManager _users;
        private readonly ILoggerFactory _loggerFactory;

        public FormatFactory(IHttpClientFactory http, IDtoService dto, IUserManager users, ILoggerFactory logger)
        {
            _httpClientFactory = http;
            _dto = dto;
            _users = users;
            _loggerFactory = logger;
        }

        public IFormat CreateFormat(HookFormat format) => format switch
        {
            HookFormat.Get => new GetFormat(GetHttpClient()),
            HookFormat.Plex => new PlexFormat(),
            _ => new DefaultFormat(GetHttpClient(), _dto, _users, _loggerFactory.CreateLogger("Webhooks.DefaultFormat")),
        };

        private HttpClient GetHttpClient()
        {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            return client;
        }
    }
}
