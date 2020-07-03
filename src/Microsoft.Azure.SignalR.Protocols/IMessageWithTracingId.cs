namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IMessageWithTracingId
    {
        ulong? TracingId { get; set; }
    }
}
