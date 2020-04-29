using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Value { get; }
        public string Id { get; }

        public Task AuthorizedTask => Task.CompletedTask;

        public AccessKey(string key)
        {
            Value = key;
            Id = key.GetHashCode().ToString();
        }
    }
}
