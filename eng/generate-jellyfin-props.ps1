param([string]$OutputPath)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "jellyfin-profiles.json") | ConvertFrom-Json
$resolvedOutput = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $repoRoot "Directory.JellyfinProfiles.props"
} elseif ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $repoRoot $OutputPath
}

function Escape([string]$Value) { [System.Security.SecurityElement]::Escape($Value) }

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("<Project>")
$lines.Add("  <!-- Generated from eng/jellyfin-profiles.json. Run eng/generate-jellyfin-props.ps1 to update. -->")
$baseVersion = Escape ([string]$config.baseVersion)
$lines.Add("")
$lines.Add("  <PropertyGroup>")
$lines.Add("    <PluginBaseVersion Condition=`"'`$(PluginBaseVersion)' == ''`">$baseVersion</PluginBaseVersion>")
$lines.Add("  </PropertyGroup>")
foreach ($profile in $config.serverProfiles) {
    $name = Escape ([string]$profile.profile)
    $build = Escape ([string]$profile.buildProfile)
    $lines.Add("")
    $lines.Add("  <PropertyGroup Condition=`"'`$(JellyfinBuildProfile)' == '' And '`$(JellyfinProfile)' == '$name'`">")
    $lines.Add("    <JellyfinBuildProfile>$build</JellyfinBuildProfile>")
    $lines.Add("  </PropertyGroup>")
}
foreach ($profile in $config.buildProfiles) {
    $name = Escape ([string]$profile.profile)
    $package = Escape ([string]$profile.packageVersion)
    $abi = Escape ([string]$profile.targetAbi)
    $framework = Escape ([string]$profile.targetFramework)
    $offset = Escape ([string]$profile.versionPatchOffset)
    $constants = Escape (($profile.constants | ForEach-Object { [string]$_ }) -join ";")
    $lines.Add("")
    $lines.Add("  <PropertyGroup Condition=`"'`$(JellyfinBuildProfile)' == '$name'`">")
    $lines.Add("    <JellyfinPackageVersion>$package</JellyfinPackageVersion>")
    $lines.Add("    <JellyfinTargetAbi>$abi</JellyfinTargetAbi>")
    $lines.Add("    <JellyfinTargetFramework>$framework</JellyfinTargetFramework>")
    $lines.Add("    <PluginVersion>`$(PluginBaseVersion).$offset</PluginVersion>")
    $lines.Add("    <JellyfinCompatibilityConstants>$constants</JellyfinCompatibilityConstants>")
    $lines.Add("  </PropertyGroup>")
}
$lines.Add("</Project>")
$outputDirectory = Split-Path -Parent $resolvedOutput
if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
[System.IO.File]::WriteAllText($resolvedOutput, (($lines -join "`n") + "`n"), [System.Text.Encoding]::ASCII)
