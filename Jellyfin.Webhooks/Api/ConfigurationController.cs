using System;
using System.Linq;
using System.Net.Mime;
using Jellyfin.Webhooks.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Webhooks.Api
{
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("Webhooks")]
    [Produces(MediaTypeNames.Application.Json)]
    public class ConfigurationController : ControllerBase
    {
        public ConfigurationController()
        {
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult Get()
        {
            var formats = Enum.GetNames(typeof(HookFormat));
            var events = Enum.GetNames(typeof(HookEvent));
            var result = new
            {
                formats,
                events
            };

            return Ok(result);
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult Delete([FromQuery] string id)
        {
            var plugin = Plugin.Instance;
            var hooks = plugin.Configuration.Hooks.Where(hook => hook.Id != id);
            plugin.Configuration.Hooks = hooks.ToArray();
            plugin.SaveConfiguration();

            return NoContent();
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult Save([FromBody] HookConfig hook)
        {
            var plugin = Plugin.Instance;
            var hooks = plugin.Configuration.Hooks.ToList();

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

            return NoContent();
        }
    }
}
