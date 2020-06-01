namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IMessageWithTracingId
    {
        // todo: change data type to long
        // todo: change the name to "Id"
        string TracingId { get; set; }
    }
}
