using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Webhooks.Formats;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Webhooks;

internal class Sender(
    ILoggerFactory logger,
    IUserManager userManager,
    IDtoService dtoService,
    IHttpClientFactory httpClientFactory
) : ISender
{
    private readonly ILogger _logger = logger.CreateLogger("Webhooks");
    private readonly FormatFactory _formatFactory = new(httpClientFactory, dtoService, userManager);

    public async Task Send(EventInfo request)
    {
        var hooks = Plugin.Instance.Configuration.Hooks
                 .Where(h => h.Events.Contains(request.Event));
        foreach (var hook in hooks)
        {
            if (request.User != null && !string.IsNullOrEmpty(hook.UserId) && request.User?.Id.ToString("N") != hook.UserId)
            {
                _logger.LogWarning("ExecuteWebhook: user mismatch, hook.UserId: {hookUserId}, request.User: {reqUser}, event: {evt}", hook.UserId, request.User, request.Event);
                continue;
            }

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
}
