using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;

namespace Jellyfin.Webhooks.Dto
{
    public class SessionInfoDto
    {
        public string Id { get; set; }
        public string Client { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string RemoteEndPoint { get; set; }
        public string ApplicationVersion { get; set; }
        public PlayerStateInfo PlayState { get; set; }

        public SessionInfoDto(SessionInfo session)
        {
            Id = session.Id;
            Client = session.Client;
            DeviceId = session.DeviceId;
            DeviceName = session.DeviceName;
            RemoteEndPoint = session.RemoteEndPoint;
            ApplicationVersion = session.ApplicationVersion;
            PlayState = session.PlayState;
        }
    }
}
