namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IHasTtl
    {
        int? Ttl { get; set; }
    }
}
