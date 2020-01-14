using System;
using System.Collections.Generic;
using Jellyfin.Webhooks.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Webhooks
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "Webhooks";

        public override Guid Id => Guid.Parse("d8ca599b-ab3c-41b0-a4ea-6de1d52b9996");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.html", GetType().Namespace),
                    EnableInMainMenu = true,
                    DisplayName = "Webhooks",
                    MenuIcon = "swap_calls"
                },
                new PluginPageInfo
                {
                    Name = "webhooks.js",
                    EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.js", GetType().Namespace)
                },
                new PluginPageInfo
                {
                    Name = "Webhooks.Editor",
                    EmbeddedResourcePath = string.Format("{0}.Configuration.editor.html", GetType().Namespace)
                },
                new PluginPageInfo
                {
                    Name = "webhooks.editor.js",
                    EmbeddedResourcePath = string.Format("{0}.Configuration.editor.js", GetType().Namespace)
                },
            };
        }
    }
}
