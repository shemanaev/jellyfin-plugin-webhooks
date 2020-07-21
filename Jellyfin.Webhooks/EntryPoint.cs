using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using Jellyfin.Webhooks.Formats;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Webhooks
{
    public class EntryPoint : IServerEntryPoint
    {
        private readonly ILogger _logger;
        private readonly ISessionManager _sessionManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;
        private readonly IServerApplicationHost _appHost;
        private readonly IHttpClient _http;
        private readonly List<Guid> _scrobbled;
        private readonly Dictionary<string, DeviceState> _deviceStates;
        private readonly FormatFactory _formatFactory;

        public EntryPoint(
            ILoggerFactory logger,
            ISessionManager sessionManager,
            IUserDataManager userDataManager,
            IUserManager userManager,
            IDtoService dtoService,
            IServerApplicationHost appHost,
            IJsonSerializer json,
            IHttpClient http
            )
        {
            _logger = logger.CreateLogger("Webhooks");
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _userManager = userManager;
            _appHost = appHost;
            _http = http;

            _scrobbled = new List<Guid>();
            _deviceStates = new Dictionary<string, DeviceState>();
            _formatFactory = new FormatFactory(json, http, dtoService, _userManager);
        }

        public void Dispose()
        {
            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _userDataManager.UserDataSaved -= OnUserDataSaved;
        }

        public Task RunAsync()
        {
            _sessionManager.PlaybackStart += OnPlaybackStart;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
            _userDataManager.UserDataSaved += OnUserDataSaved;

            return Task.CompletedTask;
        }

        private async void OnPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            await PlaybackEvent(HookEvent.Play, e.Item, e.Session, e.Users);
            SetDeviceState(e.DeviceId, DeviceState.Playing);
        }

        private async void OnPlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            await PlaybackEvent(HookEvent.Stop, e.Item, e.Session, e.Users);
            SetDeviceState(e.DeviceId, DeviceState.Stopped);
        }

        private async void OnPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            if (e.IsPaused && GetDeviceState(e.DeviceId) != DeviceState.Paused && GetDeviceState(e.DeviceId) != DeviceState.Stopped)
            {
                await PlaybackEvent(HookEvent.Pause, e.Item, e.Session, e.Users);
                SetDeviceState(e.DeviceId, DeviceState.Paused);
            }
            else if (e.IsPaused == false && GetDeviceState(e.DeviceId) == DeviceState.Paused)
            {
                await PlaybackEvent(HookEvent.Resume, e.Item, e.Session, e.Users);
                SetDeviceState(e.DeviceId, DeviceState.Playing);
            }
            else
            {
                var id = e.MediaInfo.Id;
                float percentageWatched = (float)e.Session.PlayState.PositionTicks / (float)e.Session.NowPlayingItem.RunTimeTicks * 100f;
                if (percentageWatched >= 90 && !_scrobbled.Contains(id))
                {
                    // don't scrobble virtual items
                    if (e.MediaInfo.Path == null || e.MediaInfo.LocationType == LocationType.Virtual) return;
                    await PlaybackEvent(HookEvent.Scrobble, e.Item, e.Session, e.Users);
                    _scrobbled.Add(id);
                }
            }
        }

        private async void OnUserDataSaved(object sender, UserDataSaveEventArgs e)
        {
            HookEvent evt;

            switch (e.SaveReason)
            {
                case UserDataSaveReason.TogglePlayed:
                    evt = e.UserData.Played ? HookEvent.MarkPlayed : HookEvent.MarkUnplayed;
                    break;

                case UserDataSaveReason.UpdateUserRating:
                    evt = HookEvent.Rate;
                    break;

                default:
                    return;
            }

            var user = _userManager.GetUserById(e.UserId);
            await PlaybackEvent(evt, e.Item, null, user);
        }

        private async Task PlaybackEvent(HookEvent evt, BaseItem item, SessionInfo session, User user)
        {
            if (user == null) return;
            if (item == null) return;

            await ExecuteWebhook(new EventInfo
            {
                Event = evt,
                Item = item,
                User = user,
                Session = session == null ? null : new SessionInfoDto(session),
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task PlaybackEvent(HookEvent evt, BaseItem item, SessionInfo session, List<User> users)
        {
            if (users.Count <= 0) return;
            if (item == null) return;

            foreach (var user in users)
            {
                await PlaybackEvent(evt, item, session, user);
            }
        }

        private async Task ExecuteWebhook(EventInfo request)
        {
            var hooks = Plugin.Instance.Configuration.Hooks
                .Where(h => h.Events.Contains(request.Event));
            foreach (var hook in hooks)
            {
                if (!string.IsNullOrEmpty(hook.UserId) && request.User.Id.ToString("N") != hook.UserId)
                    continue;

                var formatter = _formatFactory.CreateFormat(hook.Format);
                try
                {
                    await formatter.Format(new Uri(hook.Url), request);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Error during executing webhook: {0}", hook.Url);
                }
            }
        }

        private DeviceState GetDeviceState(string id)
        {
            if (!_deviceStates.TryGetValue(id, out var val))
            {
                val = DeviceState.Unknown;
                _deviceStates.Add(id, val);
            }

            return val;
        }

        private void SetDeviceState(string id, DeviceState state)
        {
            _deviceStates[id] = state;
        }
    }

    internal enum DeviceState
    {
        Unknown,
        Playing,
        Paused,
        Stopped,
    }
}
