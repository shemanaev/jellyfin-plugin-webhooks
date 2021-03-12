using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Jellyfin.Webhooks.Formats
{
    internal class GetFormat : IFormat
    {
        private readonly HttpClient _http;

        public GetFormat(HttpClient http)
        {
            _http = http;
        }

        public async Task Format(Uri url, EventInfo info)
        {
            var builder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["event"] = info.Event.ToString();
            query["user"] = info.User.Username;
            query["server"] = info.Server.Name;
            query["media_type"] = info.Item.MediaType;
            builder.Query = query.ToString();

            await _http.GetAsync(builder.Uri);
        }
    }
}
