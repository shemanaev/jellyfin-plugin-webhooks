using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Webhooks.Configuration
{
    public enum HookFormat
    {
        Default,
        Get,
        Plex,
    }

    public enum HookEvent
    {
        Play,
        Pause,
        Resume,
        Stop,
        Scrobble, // 90 percents

        MarkPlayed,
        MarkUnplayed,
        Rate,

        ItemAdded,
        ItemRemoved,
        ItemUpdated,

        AuthenticationSucceeded,
        AuthenticationFailed,

        SessionStarted,
        SessionEnded,

        SubtitleDownloadFailure,

        HasPendingRestartChanged,
    }

    public class HookConfig
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string UserId { get; set; }
        public HookFormat Format { get; set; }
        public HookEvent[] Events { get; set; }
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public HookConfig[] Hooks { get; set; }

        public PluginConfiguration()
        {
            Hooks = Array.Empty<HookConfig>();
        }
    }
}
