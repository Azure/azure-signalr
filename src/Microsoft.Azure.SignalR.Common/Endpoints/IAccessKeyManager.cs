namespace Microsoft.Azure.SignalR
{
    internal interface IAccessKeyManager
    {
        public void AddHubServiceEndpoint(HubServiceEndpoint endpoint);

        public void RemoveHubServiceEndpoint(HubServiceEndpoint endpoint);
    }
}
