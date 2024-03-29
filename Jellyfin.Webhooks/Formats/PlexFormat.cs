using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Webhooks.Formats
{
    internal class PlexFormat : IFormat
    {
        public PlexFormat()
        {
        }

        public async Task Format(Uri url, EventInfo info)
        {
            var eventName = GetEventName(info.Event);
            if (eventName == "unknown")
            {
                // Don't know how to handle such events
                return;
            }

            var body = new PlexFormatPayload
            {
                @event = eventName,
                user = true,
                Server = new
                {
                    title = info.Server.Name,
                    uuid = info.Server.Id
                },
            };
            if (info.User != null)
            {
                body.owner = info.User.HasPermission(Data.Enums.PermissionKind.IsAdministrator);
                body.Account = new
                {
                    id = info.User.Id,
                    title = info.User.Username
                };
            }
            if (info.Session != null)
            {
                body.Player = new
                {
                    local = true,
                    publicAddress = info.Session?.RemoteEndPoint,
                    title = info.Session?.DeviceName,
                    uuid = info.Session?.Id,
                };
            }
            if (info.Item != null)
            {
                if (info.Item.DateModified.Year == 1)
                {
                    info.Item.DateModified = info.Item.DateCreated;
                }
                body.Metadata = new
                {
                    librarySectionType = GetSectionType(info.Item),
                    guid = GetGuid(info.Item),
                    title = info.Item.Name,
                    type = GetMediaType(info.Item),
                    parentTitle = info.Item.DisplayParent?.Name,
                    grandparentTitle = info.Item.DisplayParent?.DisplayParent?.Name,
                    addedAt = ((DateTimeOffset)info.Item.DateCreated).ToUnixTimeSeconds(),
                    updatedAt = ((DateTimeOffset)info.Item.DateModified).ToUnixTimeSeconds(),
                    year = info.Item.ProductionYear,
                    duration = info.Item.RunTimeTicks / 1000,
                };
            }
            var content = JsonSerializer.Serialize(body, JsonDefaults.Options);
            var form = new MultipartFormDataContent
            {
                { new StringContent(content), "payload" }
            };

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(url, form);
            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadAsStringAsync();
            // status from SIMKL can be "OK" or "FAIL"
        }

        private static string GetEventName(HookEvent evt) => evt switch
        {
            HookEvent.Pause => "media.pause",
            HookEvent.Resume => "media.resume",
            HookEvent.Stop => "media.stop",
            HookEvent.Scrobble => "media.scrobble",
            HookEvent.Rate => "media.rate",
            HookEvent.MarkPlayed => "media.scrobble",
            HookEvent.Play => "media.play",
            HookEvent.ItemAdded => "library.new",
            _ => "unknown"
        };

        private static string GetGuid(BaseItem item)
        {
            if (item is Episode episode)
            {
                var provider = "thetvdb";
                var id = episode.Series.GetProviderId(MetadataProvider.Tvdb);
                if (string.IsNullOrEmpty(id))
                {
                    id = episode.Series.GetProviderId(MetadataProvider.Tmdb);
                    provider = "themoviedb";
                }
                return $"com.plexapp.agents.{provider}://{id}/{episode.Season.IndexNumber}/{episode.IndexNumber}?lang=en";
            }
            if (item is Movie movie)
            {
                var imdbId = movie.GetProviderId(MetadataProvider.Imdb);
                return $"com.plexapp.agents.imdb://{imdbId}?lang=en";
            }
            if (item is Audio)
            {
                return $"com.plexapp.agents.plexmusic://track/{item.Name}/{item.DisplayParent.Name}";
            }
            return $"com.plexapp.agents.unknown://{item.Name}";
        }

        private static string GetMediaType(BaseItem item)
        {
            if (item is Episode) return "episode";
            if (item is Movie) return "movie";
            if (item is Audio) return "track";
            return "unknown";
        }

        private static string GetSectionType(BaseItem item)
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
