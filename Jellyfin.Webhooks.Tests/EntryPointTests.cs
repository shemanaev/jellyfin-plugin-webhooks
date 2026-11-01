using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Webhooks.Tests;

public class EntryPointTests
{
    public static IEnumerable<object[]> EventCases()
    {
        foreach (var name in new[] { "Play", "Stop", "Progress", "UserData", "ItemAdded", "ItemRemoved", "ItemUpdated", "SessionStarted", "SessionEnded", "Subtitle", "Restart" })
        {
            foreach (var failure in new[] { "none", "sync", "async" }) yield return new object[] { name, failure };
        }
    }

    [Theory]
    [MemberData(nameof(EventCases))]
    public async Task EventHandlersDeliverNotificationsAndContainFailures(string eventName, string failure)
    {
        var fixture = new Fixture();
        await fixture.Entry.StartAsync(CancellationToken.None);
        if (failure != "none")
        {
            fixture.Sender.Setup(x => x.Send(It.IsAny<EventInfo>())).Returns(() =>
            {
                if (failure == "async") return FailAsync();
                throw new InvalidOperationException("send failed");
            });
        }

        Assert.Empty(CaptureEvent(() => fixture.Raise(eventName)));

        fixture.Sender.Verify(x => x.Send(It.IsAny<EventInfo>()), Times.AtLeastOnce);
        if (failure == "none") Assert.Empty(fixture.Logger.Invocations);
        else Assert.Equal(LogLevel.Error, Assert.Single(fixture.Logger.Invocations).Arguments[0]);
        await fixture.Entry.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(UserDataSaveReason.TogglePlayed)]
    [InlineData(UserDataSaveReason.UpdateUserRating)]
    public async Task UserDataWithEmptyUserIdIsIgnored(UserDataSaveReason reason)
    {
        var fixture = new Fixture();
        await fixture.Entry.StartAsync(CancellationToken.None);
        fixture.Users.Setup(x => x.GetUserById(Guid.Empty)).Throws(new ArgumentException("Guid can't be empty", "id"));

        Assert.Empty(CaptureEvent(() => fixture.UserData.Raise(x => x.UserDataSaved += null,
            new UserDataSaveEventArgs { UserId = Guid.Empty, SaveReason = reason, UserData = new UserItemData { Key = "item" } })));

        fixture.Users.Verify(x => x.GetUserById(Guid.Empty), Times.Never);
        Assert.Empty(fixture.Sent);
        Assert.Empty(fixture.Logger.Invocations);
        await fixture.Entry.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousSessionEndsWithoutLookingUpEmptyUser(bool hasPlayingItem)
    {
        var sessions = new Mock<ISessionManager>();
        var users = new Mock<IUserManager>();
        users.Setup(x => x.GetUserById(Guid.Empty)).Throws(new ArgumentException("Guid can't be empty", "id"));
        var host = new Mock<IServerApplicationHost>();
        host.SetupGet(x => x.ApplicationVersion).Returns(new Version(12, 0));
        var sent = new List<EventInfo>();
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<EventInfo>())).Callback<EventInfo>(sent.Add).Returns(Task.CompletedTask);
        var entry = new EntryPoint(sessions.Object, Mock.Of<IUserDataManager>(), users.Object,
            Mock.Of<ILibraryManager>(), Mock.Of<ISubtitleManager>(), host.Object, sender.Object, NullLogger<EntryPoint>.Instance);
        await entry.StartAsync(CancellationToken.None);
        var session = new SessionInfo(sessions.Object, NullLogger<SessionInfo>.Instance)
        {
            DeviceId = "disconnected-client",
            UserId = Guid.Empty,
            FullNowPlayingItem = hasPlayingItem ? new Movie() : null,
        };

        var errors = CaptureEvent(() => sessions.Raise(x => x.SessionEnded += null, new SessionEventArgs { SessionInfo = session }));

        Assert.Empty(errors);
        users.Verify(x => x.GetUserById(Guid.Empty), Times.Never);
        var notification = Assert.Single(sent);
        Assert.Equal(HookEvent.SessionEnded, notification.Event);
        Assert.Null(notification.User);
        Assert.Equal(session.DeviceId, notification.Session.DeviceId);
        await entry.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AuthenticatedSessionPreservesStopBehavior(bool alreadyStopped, bool deletedUser)
    {
        var fixture = new Fixture();
        await fixture.Entry.StartAsync(CancellationToken.None);
        if (deletedUser) fixture.Users.Setup(x => x.GetUserById(fixture.User.Id)).Returns((User)null);
        if (alreadyStopped) fixture.MarkStopped();

        Assert.Empty(fixture.End());

        Assert.Equal(alreadyStopped || deletedUser
            ? new[] { HookEvent.SessionEnded }
            : new[] { HookEvent.Stop, HookEvent.SessionEnded }, fixture.Sent.ConvertAll(x => x.Event));
        Assert.Equal(deletedUser ? null : fixture.User, fixture.Sent[^1].User);
        if (!alreadyStopped && !deletedUser) Assert.Same(fixture.Session.FullNowPlayingItem, fixture.Sent[0].Item);

        // Reusing the device must not retain the previous session's Stopped state.
        fixture.Users.Setup(x => x.GetUserById(fixture.User.Id)).Returns(fixture.User);
        fixture.Sent.Clear();
        Assert.Empty(fixture.End());
        Assert.Equal(new[] { HookEvent.Stop, HookEvent.SessionEnded }, fixture.Sent.ConvertAll(x => x.Event));
        await fixture.Entry.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendFailureIsContainedAndDeviceStateIsCleared(bool asynchronous)
    {
        var fixture = new Fixture();
        await fixture.Entry.StartAsync(CancellationToken.None);
        fixture.MarkStopped();
        fixture.Sender.Setup(x => x.Send(It.IsAny<EventInfo>())).Returns(() =>
        {
            if (asynchronous) return FailAsync();
            throw new InvalidOperationException("send failed");
        });

        Assert.Empty(fixture.End());

        fixture.RecordSends();
        Assert.Empty(fixture.End());
        Assert.Equal(new[] { HookEvent.Stop, HookEvent.SessionEnded }, fixture.Sent.ConvertAll(x => x.Event));
        await fixture.Entry.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UserLookupFailureIsContained()
    {
        var fixture = new Fixture();
        await fixture.Entry.StartAsync(CancellationToken.None);
        fixture.Users.Setup(x => x.GetUserById(fixture.User.Id)).Throws(new InvalidOperationException("lookup failed"));
        Assert.Empty(fixture.End());
        await fixture.Entry.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MissingSessionIsIgnored()
    {
        var fixture = new Fixture();
        await fixture.Entry.StartAsync(CancellationToken.None);
        Assert.Empty(CaptureEvent(() => fixture.Sessions.Raise(x => x.SessionEnded += null, new SessionEventArgs())));
        Assert.Empty(fixture.Sent);
        await fixture.Entry.StopAsync(CancellationToken.None);
    }

    private static async Task FailAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("send failed asynchronously");
    }

    private sealed class Fixture
    {
        public Mock<ISessionManager> Sessions { get; } = new();
        public Mock<IUserManager> Users { get; } = new();
        public Mock<ISender> Sender { get; } = new();
        public Mock<IUserDataManager> UserData { get; } = new();
        public Mock<ILibraryManager> Library { get; } = new();
        public Mock<ISubtitleManager> Subtitles { get; } = new();
        public Mock<IServerApplicationHost> Host { get; } = new();
        public Mock<ILogger<EntryPoint>> Logger { get; } = new();
        public List<EventInfo> Sent { get; } = [];
        public User User { get; } = new("viewer", "auth", "reset") { Id = Guid.NewGuid() };
        public SessionInfo Session { get; }
        public EntryPoint Entry { get; }

        public Fixture()
        {
            Host.SetupGet(x => x.ApplicationVersion).Returns(new Version(12, 0));
            Users.Setup(x => x.GetUserById(User.Id)).Returns(User);
            RecordSends();
            Session = new SessionInfo(Sessions.Object, NullLogger<SessionInfo>.Instance)
            {
                DeviceId = "disconnected-client",
                UserId = User.Id,
                FullNowPlayingItem = new Movie(),
            };
            Entry = new EntryPoint(Sessions.Object, UserData.Object, Users.Object,
                Library.Object, Subtitles.Object, Host.Object, Sender.Object, Logger.Object);
        }

        public void Raise(string eventName)
        {
            var progress = new PlaybackProgressEventArgs
            {
                DeviceId = Session.DeviceId, Session = Session, Item = Session.FullNowPlayingItem,
                Users = [User], MediaInfo = new MediaBrowser.Model.Dto.BaseItemDto(),
            };
            var item = new ItemChangeEventArgs { Item = Session.FullNowPlayingItem };
            var session = new SessionEventArgs { SessionInfo = Session };
            switch (eventName)
            {
                case "Play": Sessions.Raise(x => x.PlaybackStart += null, progress); break;
                case "Stop": Sessions.Raise(x => x.PlaybackStopped += null, new PlaybackStopEventArgs
                    { DeviceId = Session.DeviceId, Session = Session, Item = Session.FullNowPlayingItem, Users = [User] }); break;
                case "Progress": Sessions.Raise(x => x.PlaybackProgress += null, progress); break;
                case "UserData": UserData.Raise(x => x.UserDataSaved += null, new UserDataSaveEventArgs
                    { UserId = User.Id, Item = Session.FullNowPlayingItem, SaveReason = UserDataSaveReason.UpdateUserRating }); break;
                case "ItemAdded": Library.Raise(x => x.ItemAdded += null, Library.Object, item); break;
                case "ItemRemoved": Library.Raise(x => x.ItemRemoved += null, Library.Object, item); break;
                case "ItemUpdated": Library.Raise(x => x.ItemUpdated += null, Library.Object, item); break;
                case "SessionStarted": Sessions.Raise(x => x.SessionStarted += null, session); break;
                case "SessionEnded": Sessions.Raise(x => x.SessionEnded += null, session); break;
                case "Subtitle": Subtitles.Raise(x => x.SubtitleDownloadFailure += null, new SubtitleDownloadFailureEventArgs
                    { Item = Session.FullNowPlayingItem, Exception = new Exception("subtitle failed") }); break;
                case "Restart": Host.Raise(x => x.HasPendingRestartChanged += null, EventArgs.Empty); break;
                default: throw new ArgumentOutOfRangeException(nameof(eventName));
            }
        }

        public void RecordSends() => Sender.Setup(x => x.Send(It.IsAny<EventInfo>()))
            .Returns<EventInfo>(request => { Sent.Add(request); return Task.CompletedTask; });

        public List<Exception> End() => CaptureEvent(() => Sessions.Raise(x => x.SessionEnded += null,
            new SessionEventArgs { SessionInfo = Session }));

        public void MarkStopped() => Assert.Empty(CaptureEvent(() => Sessions.Raise(x => x.PlaybackStopped += null,
            new PlaybackStopEventArgs { DeviceId = Session.DeviceId, Session = Session, Users = [] })));
    }

    // Pump the real async-void event, including exceptions posted by AsyncVoidMethodBuilder.
    private static List<Exception> CaptureEvent(Action raise)
    {
        var previous = SynchronizationContext.Current;
        using var context = new EventContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            raise();
            return context.Drain();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class EventContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new();
        private int _operations;
        public override void OperationStarted() => Interlocked.Increment(ref _operations);
        public override void OperationCompleted() => Interlocked.Decrement(ref _operations);
        public override void Post(SendOrPostCallback callback, object state) => _queue.Add(() => callback(state));

        public List<Exception> Drain()
        {
            var errors = new List<Exception>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (_operations > 0 || _queue.Count > 0)
            {
                Assert.True(DateTime.UtcNow < deadline, "Session event did not complete.");
                if (!_queue.TryTake(out var callback, 10)) continue;
                try { callback(); }
                catch (Exception error) { errors.Add(error); }
            }
            return errors;
        }

        public void Dispose() => _queue.Dispose();
    }
}
