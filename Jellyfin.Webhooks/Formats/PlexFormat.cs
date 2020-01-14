using System;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Webhooks.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Webhooks.Formats
{
    internal class PlexFormat : IFormat
    {
        private readonly IJsonSerializer _json;

        public PlexFormat(IJsonSerializer json)
        {
            _json = json;
        }

        public async Task Format(Uri url, EventInfo info)
        {
            var body = new PlexFormatPayload
            {
                @event = GetEventName(info.Event),
                user = true,
                owner = info.User.Policy.IsAdministrator,
                Account = new
                {
                    id = info.User.Id,
                    title = info.User.Name
                },
                Server = new
                {
                    title = info.Server.Name,
                    uuid = info.Server.Id
                },
                Player = new
                {
                    local = true,
                    publicAddress = info.Session?.RemoteEndPoint,
                    title = info.Session?.DeviceName,
                    uuid = info.Session?.Id,
                },
                Metadata = new
                {
                    librarySectionType = GetSectionType(info.Item),
                    guid = GetGuid(info.Item),
                    title = info.Item.Name,
                    type = GetMediaType(info.Item),
                    parentTitle = info.Item.DisplayParent.Name,
                    grandparentTitle = info.Item.DisplayParent.DisplayParent.Name,
                    addedAt = ((DateTimeOffset)info.Item.DateCreated).ToUnixTimeSeconds(),
                    updatedAt = ((DateTimeOffset)info.Item.DateModified).ToUnixTimeSeconds(),
                    year = info.Item.ProductionYear,
                    duration = info.Item.RunTimeTicks / 1000,
                }
            };
            var content = _json.SerializeToString(body);
            var form = new MultipartFormDataContent
            {
                { new StringContent(content), "payload" }
            };

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.PostAsync(url, form);
                response.EnsureSuccessStatusCode();
                var status = await response.Content.ReadAsStringAsync();
                // status from SIMKL can be "OK" or "FAIL"
            }
        }

        private string GetEventName(HookEvent evt)
        {
            switch (evt)
            {
                case HookEvent.Pause:
                    return "media.pause";

                case HookEvent.Resume:
                    return "media.resume";

                case HookEvent.Stop:
                    return "media.stop";

                case HookEvent.Scrobble:
                    return "media.scrobble";

                case HookEvent.Rate:
                    return "media.rate";

                case HookEvent.MarkPlayed:
                    return "media.scrobble";

                default:
                    return "media.play";
            }
        }

        private string GetGuid(BaseItem item)
        {
            if (item is Episode episode)
            {
                var provider = "thetvdb";
                var id = episode.Series.GetProviderId(MetadataProviders.Tvdb);
                if (string.IsNullOrEmpty(id))
                {
                    id = episode.Series.GetProviderId(MetadataProviders.Tmdb);
                    provider = "themoviedb";
                }
                return $"com.plexapp.agents.{provider}://{id}/{episode.Season.IndexNumber}/{episode.IndexNumber}?lang=en";
            }
            if (item is Movie movie)
            {
                var imdbId = movie.GetProviderId(MetadataProviders.Imdb);
                return $"com.plexapp.agents.imdb://{imdbId}?lang=en";
            }
            if (item is Audio)
            {
                return $"com.plexapp.agents.plexmusic://track/{item.Name}/{item.DisplayParent.Name}";
            }
            return $"com.plexapp.agents.unknown://{item.Name}";
        }

        private string GetMediaType(BaseItem item)
        {
            if (item is Episode) return "episode";
            if (item is Movie) return "movie";
            if (item is Audio) return "track";
            return "unknown";
        }

        private string GetSectionType(BaseItem item)
        {
            if (item is Episode) return "show";
            if (item is Movie) return "movie";
            if (item is Audio) return "artist";
            return "unknown";
        }
    }

    public class PlexFormatPayload
    {
        public string @event { get; set; }
        public bool user { get; set; }
        public bool owner { get; set; }
        public object Account { get; set; }
        public object Server { get; set; }
        public object Player { get; set; }
        public object Metadata { get; set; }
    }
}
