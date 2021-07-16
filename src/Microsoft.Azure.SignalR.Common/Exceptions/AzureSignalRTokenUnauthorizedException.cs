namespace Microsoft.Azure.SignalR.Common
{
    public class AzureSignalRTokenUnauthorizedException : AzureSignalRException
    {
        internal AzureSignalRTokenUnauthorizedException(string message) : base(message)
        {
        }
    }
}
