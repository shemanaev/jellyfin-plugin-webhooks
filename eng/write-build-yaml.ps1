param(
    [Parameter(Mandatory = $true)][string]$Profile,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Jellyfin.Webhooks/Jellyfin.Webhooks.csproj"

function Get-Property([string]$Name) {
    $value = (& dotnet msbuild $project -nologo -p:JellyfinProfile=$Profile -getProperty:$Name).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        throw "Unable to resolve MSBuild property '$Name' for Jellyfin $Profile."
    }
    $value
}

$artifactLines = (Get-Property "PluginArtifacts") -split ";" | Where-Object { $_ } | ForEach-Object { "  - `"$($_.Trim())`"" }
$yaml = @"
---
name: "$(Get-Property "PluginName")"
guid: "$(Get-Property "PluginGuid")"
version: "$(Get-Property "PluginVersion")"
targetAbi: "$(Get-Property "JellyfinTargetAbi")"
framework: "$(Get-Property "JellyfinTargetFramework")"
overview: "$(Get-Property "PluginOverview")"
description: "$(Get-Property "PluginDescription")"
category: "$(Get-Property "PluginCategory")"
owner: "$(Get-Property "PluginOwner")"
artifacts:
$($artifactLines -join "`n")
changelog: "Jellyfin $Profile compatibility"
"@
[System.IO.File]::WriteAllText($OutputPath, ($yaml + "`n"), [System.Text.Encoding]::ASCII)
