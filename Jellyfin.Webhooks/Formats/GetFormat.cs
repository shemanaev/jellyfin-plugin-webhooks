using System;
using System.Threading.Tasks;
using System.Web;
using MediaBrowser.Common.Net;

namespace Jellyfin.Webhooks.Formats
{
    internal class GetFormat : IFormat
    {
        private readonly IHttpClient _http;

        public GetFormat(IHttpClient http)
        {
            _http = http;
        }

        public async Task Format(Uri url, EventInfo info)
        {
            var builder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["event"] = info.Event.ToString();
            query["user"] = info.User.Name;
            query["server"] = info.Server.Name;
            query["media_type"] = info.Item.MediaType;
            builder.Query = query.ToString();

            var options = new HttpRequestOptions
            {
                RequestContentType = "text/html",
                LogErrorResponseBody = true,
                EnableDefaultUserAgent = true,
                Url = builder.Uri.ToString(),
            };
            await _http.SendAsync(options, "GET");
        }
    }
}
