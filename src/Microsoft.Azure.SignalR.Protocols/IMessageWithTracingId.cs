namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IMessageWithTracingId
    {
        // todo: change data type to long
        string TracingId { get; set; }
    }
}
