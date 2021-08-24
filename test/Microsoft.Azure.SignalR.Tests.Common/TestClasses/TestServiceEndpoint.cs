namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal class TestServiceEndpoint : ServiceEndpoint
    {
        private const string _defaultConnectionString = "Endpoint=https://local;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ;Version=1.0";

        public TestServiceEndpoint(string connectionString = null) : base(connectionString ?? _defaultConnectionString)
        {
        }
    }
}