#if JELLYFIN_USER_IN_DATA_ENTITIES
global using JellyfinUser = Jellyfin.Data.Entities.User;
#else
global using JellyfinUser = Jellyfin.Database.Implementations.Entities.User;
#endif
