param(
    [ValidateSet("BuildProfiles", "ServerProfiles", "GitHubBuildMatrix", "SmokeMatrix")]
    [string]$Format = "BuildProfiles"
)

$ErrorActionPreference = "Stop"
$config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "jellyfin-profiles.json") | ConvertFrom-Json

switch ($Format) {
    "BuildProfiles" { $config.buildProfiles | ForEach-Object { $_.profile } }
    "ServerProfiles" { $config.serverProfiles | ForEach-Object { $_.profile } }
    "GitHubBuildMatrix" {
        [ordered]@{ include = @($config.buildProfiles | ForEach-Object {
            [ordered]@{
                "jellyfin-profile" = $_.profile
                "dotnet-target" = $_.targetFramework
            }
        }) } | ConvertTo-Json -Compress -Depth 4
    }
    "SmokeMatrix" {
        [ordered]@{ include = @($config.serverProfiles | ForEach-Object {
            $server = $_
            $build = $config.buildProfiles | Where-Object profile -eq $server.buildProfile | Select-Object -First 1
            [ordered]@{
                "jellyfin-profile" = $server.profile
                "jellyfin-build-profile" = $server.buildProfile
                "dotnet-target" = $build.targetFramework
            }
        }) } | ConvertTo-Json -Compress -Depth 4
    }
}
