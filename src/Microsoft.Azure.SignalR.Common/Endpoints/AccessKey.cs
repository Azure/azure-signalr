namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Value { get; set; }
        public string Id { get; set; }

        public AccessKey(string key, string keyId = null)
        {
            Value = key;
            if (string.IsNullOrEmpty(keyId))
            {
                Id = key.GetHashCode().ToString();
            }
        }
    }
}
