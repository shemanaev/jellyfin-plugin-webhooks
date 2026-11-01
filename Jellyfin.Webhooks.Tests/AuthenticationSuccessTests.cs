using System;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Webhooks.Configuration;
using Jellyfin.Webhooks.Notifiers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Events.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Moq;
using Xunit;

namespace Jellyfin.Webhooks.Tests;

public class AuthenticationSuccessTests
{
    [Theory]
    [InlineData("null-event")]
    [InlineData("null-user")]
    [InlineData("empty-id")]
    public async Task MissingUserIsIgnored(string input)
    {
        var users = new Mock<IUserManager>(MockBehavior.Strict);
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var notifier = new AuthenticationSuccessNotifier(Mock.Of<IServerApplicationHost>(), users.Object, sender.Object);
        var args = input == "null-event" ? null : new AuthenticationResultEventArgs(new AuthenticationResult
        {
            User = input == "empty-id" ? new UserDto { Id = Guid.Empty } : null,
        });

        await notifier.OnEvent(args);

        users.VerifyNoOtherCalls();
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidUserIsDeliveredWithOptionalSession(bool hasSession)
    {
        var user = new User("viewer", "auth", "reset") { Id = Guid.NewGuid() };
        var users = new Mock<IUserManager>();
        users.Setup(x => x.GetUserById(user.Id)).Returns(user);
        var host = new Mock<IServerApplicationHost>();
        host.SetupGet(x => x.ApplicationVersion).Returns(new Version(12, 0));
        var sender = new Mock<ISender>();
        EventInfo sent = null;
        sender.Setup(x => x.Send(It.IsAny<EventInfo>())).Callback<EventInfo>(x => sent = x).Returns(Task.CompletedTask);
        var notifier = new AuthenticationSuccessNotifier(host.Object, users.Object, sender.Object);

        await notifier.OnEvent(new AuthenticationResultEventArgs(new AuthenticationResult
        {
            User = new UserDto { Id = user.Id },
            SessionInfo = hasSession ? new SessionInfoDto { DeviceId = "client" } : null,
        }));

        Assert.NotNull(sent);
        Assert.Equal(HookEvent.AuthenticationSucceeded, sent.Event);
        Assert.Same(user, sent.User);
        Assert.Equal(hasSession ? "client" : null, sent.Session?.DeviceId);
    }
}
