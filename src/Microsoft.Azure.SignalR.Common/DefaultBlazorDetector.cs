using System.Collections.Concurrent;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultBlazorDetector: IBlazorDetector
    {
        private readonly ConcurrentDictionary<string, bool> _blazor = new ConcurrentDictionary<string, bool>();

        public bool IsBlazor(string hubName)
        {
            _blazor.TryGetValue(hubName, out var isBlazor);
            return isBlazor;
        }

        public bool TrySetBlazor(string hubName, bool isBlazor)
        {
            return _blazor.TryAdd(hubName, isBlazor);
        }
    }
}
