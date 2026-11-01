using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Webhooks.Tests;

public class PluginIdentityTests
{
    [Fact]
    public void PackageIdentityMatchesExistingPluginIdentity()
    {
        // Id has no constructor dependencies. Check the compiled plugin against
        // the same MSBuild property used to generate release package metadata.
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var packageGuid = typeof(PluginIdentityTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PackageGuid").Value;

        Assert.Equal(Guid.Parse("d8ca599b-ab3c-41b0-a4ea-6de1d52b9996"), plugin.Id);
        Assert.Equal(plugin.Id, Guid.Parse(packageGuid));
    }
}
