using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.SignalR
{
    public class AzureAdOptions
    {
        public readonly X509Certificate2 ClientCert;

        public string ClientId { get; private set; }

        public string ClientSecret { get; private set; }

        public string TenantId { get; private set; }

        public bool Enabled { get; } = false;

        public string Instance { get; set; } = "https://login.microsoftonline.com/{0}";

        public AzureAdOptions()
        {
        }

        public AzureAdOptions(string clientId, string clientSecret, string tenantId)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            TenantId = tenantId;
            Enabled = true;
        }

        public AzureAdOptions(string clientId, X509Certificate2 clientCert, string tenantId)
        {
            ClientId = clientId;
            ClientCert = clientCert;
            TenantId = tenantId;
            Enabled = true;
        }
    }
}
