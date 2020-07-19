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
