namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IMessageWithTracingId
    {
        long TracingId { get; set; }
    }
}
