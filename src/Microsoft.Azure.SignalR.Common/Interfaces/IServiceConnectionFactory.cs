namespace Microsoft.Azure.SignalR
{
    internal interface IServiceConnectionFactory
    {
        IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, AckHandler ackHandler, ServiceConnectionType type);
    }
}
