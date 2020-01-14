using System;
using System.Linq;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

// Useful links for json requests:
// https://github.com/jellyfin/jellyfin/blob/b5d0bd0d764982c8df2c341c1f38c4d0636409c3/MediaBrowser.Api/ConfigurationService.cs#L36
// https://github.com/jellyfin/jellyfin/blob/b5d0bd0d764982c8df2c341c1f38c4d0636409c3/MediaBrowser.Api/ConfigurationService.cs#L121

namespace Jellyfin.Webhooks.Configuration
{
    [Route("/Webhooks", "POST")]
    [Authenticated(Roles = "Admin")]
    public class SaveWebhook : HookConfig, IReturnVoid
    {
    }

    [Route("/Webhooks", "DELETE")]
    [Authenticated(Roles = "Admin")]
    public class DeleteWebhook : IReturnVoid
    {
        public string Id { get; set; }
    }

    public class ConfigurationApi : IService
    {
        private readonly IJsonSerializer _json;

        public ConfigurationApi(IJsonSerializer json)
        {
            _json = json;
        }

        public void Delete(DeleteWebhook request)
        {
            var plugin = Plugin.Instance;
            var hooks = plugin.Configuration.Hooks.Where(hook => hook.Id != request.Id);
            plugin.Configuration.Hooks = hooks.ToArray();
            plugin.SaveConfiguration();
        }

        public void Post(SaveWebhook request)
        {
            var plugin = Plugin.Instance;
            var hooks = plugin.Configuration.Hooks.ToList();
            var hook = _json.DeserializeFromString<HookConfig>(_json.SerializeToString(request));

            if (string.IsNullOrWhiteSpace(hook.Id))
            {
                hook.Id = Guid.NewGuid().ToString("N");
                hooks.Add(hook);
            }
            else
            {
                var index = hooks.FindIndex(h => h.Id == hook.Id);
                if (index != -1)
                {
                    hooks[index] = hook;
                }
            }

            plugin.Configuration.Hooks = hooks.ToArray();
            plugin.SaveConfiguration();
        }
    }
}
