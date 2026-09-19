# Webhooks for Jellyfin

Supports couple of request formats:

  - **Default** - _native_ Jellyfin payload
  - **Get** - simple `GET` requests for low memory devices like IoT
  - **Plex** - Plex-_ish_ type of payload. Enough to scrobble on services like SIMKL


## Installation

Add repository with my plugins from [jellyfin-plugin-repo](https://github.com/shemanaev/jellyfin-plugin-repo).


## Debugging

Define `JellyfinHome` environment variable pointing to Jellyfin distribution to be able to run debug configuration.
Included `docker-compose.yml` can be used to start webhook testing app on [localhost:8084](http://localhost:8084).

## Compatibility builds

The same source tree builds against the last Jellyfin 10 release and the Jellyfin 12 ABI:

```powershell
.\eng\build-plugin.ps1
```

Artifacts and profile-specific `build.yaml` manifests are written to `artifacts/<profile>`.
To start real Jellyfin containers and verify that the plugin loads without ABI/type errors:

```powershell
.\eng\smoke-test-jellyfin-docker.ps1
```

The Jellyfin 12.0 artifact is smoke-tested on both 12.0 and the latest 12.1 server.
Pass `-Profile 12.1` to smoke-test only the latest server (the script selects the compatible 12.0 build automatically).
