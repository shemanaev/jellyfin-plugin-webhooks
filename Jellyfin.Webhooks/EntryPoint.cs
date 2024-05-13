using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Dto;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Webhooks
{
    public class EntryPoint(
        ISessionManager sessionManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        ILibraryManager libraryManager,
        ISubtitleManager subtitleManager,
        IServerApplicationHost appHost,
        ISender _sender
    ) : IHostedService
    {
        private readonly List<Guid> _scrobbled = [];
        private readonly Dictionary<string, DeviceState> _deviceStates = [];

        public Task StopAsync(CancellationToken cancellationToken)
        {
            sessionManager.PlaybackStart -= OnPlaybackStart;
            sessionManager.PlaybackStopped -= OnPlaybackStopped;
            sessionManager.PlaybackProgress -= OnPlaybackProgress;
            sessionManager.SessionStarted -= OnSessionStarted;
            sessionManager.SessionEnded -= OnSessionEnded;

            userDataManager.UserDataSaved -= OnUserDataSaved;

            libraryManager.ItemAdded -= OnItemAdded;
            libraryManager.ItemRemoved -= OnItemRemoved;
            libraryManager.ItemUpdated -= OnItemUpdated;

            subtitleManager.SubtitleDownloadFailure -= OnSubtitleDownloadFailure;

            appHost.HasPendingRestartChanged -= HasPendingRestartChanged;

            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            sessionManager.PlaybackStart += OnPlaybackStart;
            sessionManager.PlaybackStopped += OnPlaybackStopped;
            sessionManager.PlaybackProgress += OnPlaybackProgress;
            sessionManager.SessionStarted += OnSessionStarted;
            sessionManager.SessionEnded += OnSessionEnded;

            userDataManager.UserDataSaved += OnUserDataSaved;

            libraryManager.ItemAdded += OnItemAdded;
            libraryManager.ItemRemoved += OnItemRemoved;
            libraryManager.ItemUpdated += OnItemUpdated;

            subtitleManager.SubtitleDownloadFailure += OnSubtitleDownloadFailure;

            appHost.HasPendingRestartChanged += HasPendingRestartChanged;

            return Task.CompletedTask;
        }

        private async void OnPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            SetDeviceState(e.DeviceId, DeviceState.Playing);
            await PlaybackEvent(HookEvent.Play, e.Item, e.Session, e.Users);
        }

        private async void OnPlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
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

            var user = userManager.GetUserById(e.UserId);
            await PlaybackEvent(evt, e.Item, null, user);
        }

        private async void OnItemAdded(object sender, ItemChangeEventArgs e)
        {
            await LibraryEvent(HookEvent.ItemAdded, e.Item, e.UpdateReason);
        }

        private async void OnItemRemoved(object sender, ItemChangeEventArgs e)
        {
            await LibraryEvent(HookEvent.ItemRemoved, e.Item, e.UpdateReason);
        }

        private async void OnItemUpdated(object sender, ItemChangeEventArgs e)
        {
            await LibraryEvent(HookEvent.ItemUpdated, e.Item, e.UpdateReason);
        }

        private async void OnSessionStarted(object sender, SessionEventArgs e)
        {
            await SessionEvent(HookEvent.SessionStarted, e.SessionInfo);
        }

        private async void OnSessionEnded(object sender, SessionEventArgs e)
        {
            if (GetDeviceState(e.SessionInfo.DeviceId) != DeviceState.Stopped)
            {
                var user = userManager.GetUserById(e.SessionInfo.UserId);
                await PlaybackEvent(HookEvent.Stop, e.SessionInfo.FullNowPlayingItem, e.SessionInfo, user);
            }

            await SessionEvent(HookEvent.SessionEnded, e.SessionInfo);
            ClearDeviceState(e.SessionInfo.DeviceId);
        }

        private async void OnSubtitleDownloadFailure(object sender, SubtitleDownloadFailureEventArgs e)
        {
            await _sender.Send(new EventInfo
            {
                Event = HookEvent.SubtitleDownloadFailure,
                Item = e.Item,
                AdditionalData = e.Exception,
                Server = new ServerInfoDto
                {
                    Id = appHost.SystemId,
                    Name = appHost.FriendlyName,
                    Version = appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async void HasPendingRestartChanged(object sender, EventArgs e)
        {
            await _sender.Send(new EventInfo
            {
                Event = HookEvent.HasPendingRestartChanged,
                Server = new ServerInfoDto
                {
                    Id = appHost.SystemId,
                    Name = appHost.FriendlyName,
                    Version = appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task SessionEvent(HookEvent evt, SessionInfo session)
        {
            if (session == null) return;

            User user = null;
            if (session.UserId != Guid.Empty)
            {
                user = userManager.GetUserById(session.UserId);
            }

            await _sender.Send(new EventInfo
            {
                Event = evt,
                User = user,
                Session = new SessionInfoDto(session),
                Server = new ServerInfoDto
                {
                    Id = appHost.SystemId,
                    Name = appHost.FriendlyName,
                    Version = appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task LibraryEvent(HookEvent evt, BaseItem item, ItemUpdateType updateReason)
        {
            if (item == null) return;
            if (item.IsVirtualItem) return;

            await _sender.Send(new EventInfo
            {
                Event = evt,
                Item = item,
                AdditionalData = updateReason,
                Server = new ServerInfoDto
                {
                    Id = appHost.SystemId,
                    Name = appHost.FriendlyName,
                    Version = appHost.ApplicationVersion.ToString(),
                },
            });
        }

        private async Task PlaybackEvent(HookEvent evt, BaseItem item, SessionInfo session, User user)
        {
            if (user == null) return;
            if (item == null) return;

            await _sender.Send(new EventInfo
            {
                Event = evt,
                Item = item,
                User = user,
                Session = session == null ? null : new SessionInfoDto(session),
                Server = new ServerInfoDto
                {
                    Id = appHost.SystemId,
                    Name = appHost.FriendlyName,
                    Version = appHost.ApplicationVersion.ToString(),
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
