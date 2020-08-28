namespace Microsoft.Azure.SignalR
{
    internal interface IBlazorDetector
    {
        public bool IsBlazor(string hubName);

        public bool TrySetBlazor(string hubName, bool isBlazor);
    }
}
