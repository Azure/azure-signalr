namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IMessageWithTracingId
    {
        string TracingId { get; set; }
    }
}
