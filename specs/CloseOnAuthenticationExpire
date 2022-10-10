Feature request: #1690

* Service runtime:
    * Rresponsible for closing the expired connections so that we can also support the feature in serverless mode. Uses the "exp" field of JWT as the expiration time.
    * Like dotnet/aspnetcore/pull/32431 does, runtime will scan all the client connections every seconds and close the expired connections.
    * When closing an expired connection, send close message to SDK which indicates the client is closed for auth expires. (Requires protocol changes), send close message to client which allows reconnect.
 
* SDK
    * Uses claim to indicate whether the client should be closed on auth expires.
    * Provides hooks for server to be notified when client is closed on auth expires in the same way as dotnet/aspnetcore/pull/32431 does.
    * Questions: currently SDK provides an option `ServiceOptions.AccessTokenLifetime` to set the access token lifetime. The option is shared among all hubs and all clients. Do we have a need for specifying the value for a hub or for a client?
