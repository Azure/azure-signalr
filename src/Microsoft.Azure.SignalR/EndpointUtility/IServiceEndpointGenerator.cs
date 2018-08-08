namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointGenerator
    {
        string GetClientAudience(string hubName);
        string GetClientEndpoint(string hubName);
        string GetServerAudience(string hubName);
        string GetServerEndpoint(string hubName);
    }
}
