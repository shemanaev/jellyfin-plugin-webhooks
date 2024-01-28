using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Events;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using Jellyfin.Webhooks.Formats;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Webhooks
{
    public class EntryPoint : IServerEntryPoint
    {
        private readonly ILogger _logger;
        private readonly ISessionManager _sessionManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ISubtitleManager _subtitleManager;
        private readonly IServerApplicationHost _appHost;
        private readonly List<Guid> _scrobbled;
        private readonly Dictionary<string, DeviceState> _deviceStates;
        private readonly FormatFactory _formatFactory;

        public EntryPoint(
            ILoggerFactory logger,
            ISessionManager sessionManager,
            IUserDataManager userDataManager,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ISubtitleManager subtitleManager,
            IDtoService dtoService,
            IServerApplicationHost appHost,
            IHttpClientFactory httpClientFactory
            )
        {
            _logger = logger.CreateLogger("Webhooks");
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _subtitleManager = subtitleManager;
            _appHost = appHost;

            _scrobbled = new List<Guid>();
            _deviceStates = new Dictionary<string, DeviceState>();
            _formatFactory = new FormatFactory(httpClientFactory, dtoService, _userManager, logger);
        }

        public void Dispose()
        {
            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _sessionManager.AuthenticationFailed -= OnAuthenticationFailed;
            _sessionManager.AuthenticationSucceeded -= OnAuthenticationSucceeded;
            _sessionManager.SessionStarted -= OnSessionStarted;
            _sessionManager.SessionEnded -= OnSessionEnded;

            _userDataManager.UserDataSaved -= OnUserDataSaved;

            _libraryManager.ItemAdded -= OnItemAdded;
            _libraryManager.ItemRemoved -= OnItemRemoved;
            _libraryManager.ItemUpdated -= OnItemUpdated;

            _subtitleManager.SubtitleDownloadFailure -= OnSubtitleDownloadFailure;

            _appHost.HasPendingRestartChanged -= HasPendingRestartChanged;
        }

        public Task RunAsync()
        {
            _sessionManager.PlaybackStart += OnPlaybackStart;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
            _sessionManager.AuthenticationFailed += OnAuthenticationFailed;
            _sessionManager.AuthenticationSucceeded += OnAuthenticationSucceeded;
            _sessionManager.SessionStarted += OnSessionStarted;
            _sessionManager.SessionEnded += OnSessionEnded;

            _userDataManager.UserDataSaved += OnUserDataSaved;

            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemRemoved += OnItemRemoved;
            _libraryManager.ItemUpdated += OnItemUpdated;

            _subtitleManager.SubtitleDownloadFailure += OnSubtitleDownloadFailure;

            _appHost.HasPendingRestartChanged += HasPendingRestartChanged;

            return Task.CompletedTask;
        }

        private async void OnPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            _logger.LogInformation("OnPlaybackStart");
            SetDeviceState(e.DeviceId, DeviceState.Playing);
            await PlaybackEvent(HookEvent.Play, e.Item, e.Session, e.Users);
        }

        private async void OnPlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            _logger.LogInformation("OnPlaybackStopped");
            SetDeviceState(e.DeviceId, DeviceState.Stopped);
            await PlaybackEvent(HookEvent.Stop, e.Item, e.Session, e.Users);
        }

        private async void OnPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            if (e.IsPaused && GetDeviceState(e.DeviceId) != DeviceState.Paused && GetDeviceState(e.DeviceId) != DeviceState.Stopped)
            {
                SetDeviceState(e.DeviceId, DeviceState.Paused);
                await PlaybackEvent(HookEvent.Pause, e.Item, e.Session, e.Users);
            }
            else if (e.IsPaused == false && GetDeviceState(e.DeviceId) == DeviceState.Paused)
            {
                SetDeviceState(e.DeviceId, DeviceState.Playing);
                await PlaybackEvent(HookEvent.Resume, e.Item, e.Session, e.Users);
            }
            else
            {
                await PlaybackEvent(HookEvent.Progress, e.Item, e.Session, e.Users);

                // don't scrobble virtual items
                if (e.MediaInfo.Path == null
                    || !e.MediaInfo.LocationType.HasValue || e.MediaInfo.LocationType == LocationType.Virtual
                    || !e.Session.PlayState.PositionTicks.HasValue
                    || !e.Session.NowPlayingItem.RunTimeTicks.HasValue) return;

                var id = e.MediaInfo.Id;
                float percentageWatched = (float)e.Session.PlayState.PositionTicks / (float)e.Session.NowPlayingItem.RunTimeTicks * 100f;
                if (percentageWatched >= 90 && !_scrobbled.Contains(id))
                {
                    _scrobbled.Add(id);
                    await PlaybackEvent(HookEvent.Scrobble, e.Item, e.Session, e.Users);
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

            _logger.LogInformation("OnUserDataSaved");
            var user = _userManager.GetUserById(e.UserId);
            await PlaybackEvent(evt, e.Item, null, user);
        }

        private async void OnItemAdded(object sender, ItemChangeEventArgs e)
        {
            _logger.LogInformation("OnItemAdded");
            await LibraryEvent(HookEvent.ItemAdded, e.Item, e.UpdateReason);
        }

        private async void OnItemRemoved(object sender, ItemChangeEventArgs e)
        {
            _logger.LogInformation("OnItemRemoved");
            await LibraryEvent(HookEvent.ItemRemoved, e.Item, e.UpdateReason);
        }

        private async void OnItemUpdated(object sender, ItemChangeEventArgs e)
        {
            _logger.LogInformation("OnItemUpdated");
            await LibraryEvent(HookEvent.ItemUpdated, e.Item, e.UpdateReason);
        }

        private async void OnAuthenticationSucceeded(object sender, GenericEventArgs<AuthenticationResult> e)
        {
            _logger.LogInformation("OnAuthenticationSucceeded");
            var user = _userManager.GetUserById(e.Argument.User.Id);
            await ExecuteWebhook(new EventInfo
            {
                Event = HookEvent.AuthenticationSucceeded,
                Session = new SessionInfoDto(e.Argument.SessionInfo),
                User = user,
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async void OnAuthenticationFailed(object sender, GenericEventArgs<AuthenticationRequest> e)
        {
            _logger.LogInformation("OnAuthenticationFailed");
            await ExecuteWebhook(new EventInfo
            {
                Event = HookEvent.AuthenticationFailed,
                AdditionalData = e.Argument,
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async void OnSessionStarted(object sender, SessionEventArgs e)
        {
            _logger.LogInformation("OnSessionStarted");
            await SessionEvent(HookEvent.SessionStarted, e.SessionInfo);
        }

        private async void OnSessionEnded(object sender, SessionEventArgs e)
        {
            _logger.LogInformation("OnSessionEnded");
            if (GetDeviceState(e.SessionInfo.DeviceId) != DeviceState.Stopped)
            {
                var user = _userManager.GetUserById(e.SessionInfo.UserId);
                await PlaybackEvent(HookEvent.Stop, e.SessionInfo.FullNowPlayingItem, e.SessionInfo, user);
            }

            await SessionEvent(HookEvent.SessionEnded, e.SessionInfo);
            ClearDeviceState(e.SessionInfo.DeviceId);
        }

        private async void OnSubtitleDownloadFailure(object sender, SubtitleDownloadFailureEventArgs e)
        {
            _logger.LogInformation("OnSubtitleDownloadFailure");
            await ExecuteWebhook(new EventInfo
            {
                Event = HookEvent.SubtitleDownloadFailure,
                Item = e.Item,
                AdditionalData = e.Exception,
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async void HasPendingRestartChanged(object sender, EventArgs e)
        {
            _logger.LogInformation("HasPendingRestartChanged");
            await ExecuteWebhook(new EventInfo
            {
                Event = HookEvent.HasPendingRestartChanged,
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task SessionEvent(HookEvent evt, SessionInfo session)
        {
            if (session == null) return;

            User user = null;
            if (session.UserId != Guid.Empty)
            {
                user = _userManager.GetUserById(session.UserId);
            }

            _logger.LogInformation("SessionEvent");
            await ExecuteWebhook(new EventInfo
            {
                Event = evt,
                User = user,
                Session = new SessionInfoDto(session),
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task LibraryEvent(HookEvent evt, BaseItem item, ItemUpdateType updateReason)
        {
            if (item == null) return;
            if (item.IsVirtualItem) return;

            _logger.LogInformation("LibraryEvent");
            await ExecuteWebhook(new EventInfo
            {
                Event = evt,
                Item = item,
                AdditionalData = updateReason,
                Server = new ServerInfoDto
                {
                    Id = _appHost.SystemId,
                    Name = _appHost.FriendlyName,
                    Version = _appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task PlaybackEvent(HookEvent evt, BaseItem item, SessionInfo session, User user)
        {
            if (user == null) return;
            if (item == null) return;

            _logger.LogInformation("PlaybackEvent");
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
                if (!string.IsNullOrEmpty(hook.UserId) && request.User?.Id.ToString("N") != hook.UserId)
                    continue;

                var formatter = _formatFactory.CreateFormat(hook.Format);
                try
                {
                    _logger.LogInformation("ExecuteWebhook: {id}, format: {format}, url: {url}", hook.Id, hook.Format, hook.Url);
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

        private void ClearDeviceState(string id)
        {
            _deviceStates.Remove(id);
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
