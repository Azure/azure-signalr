using Microsoft.Identity.Client;
using System.Globalization;

namespace Microsoft.Azure.SignalR.Common.Utilities
{
    internal class AadHelper
    {
        public static IConfidentialClientApplication BuildApplication(AzureAdOptions options)
        {
            if (options.Enabled)
            {
                var builder = ConfidentialClientApplicationBuilder.Create(options.ClientId);
                var authority = string.Format(CultureInfo.InvariantCulture, options.Instance, options.TenantId);
                builder.WithAuthority(new System.Uri(authority));
                if (options.ClientCert != null)
                {
                    builder.WithCertificate(options.ClientCert);
                }
                else
                {
                    builder.WithClientSecret(options.ClientSecret);
                }
                return builder.Build();
            }
            else
            {
                throw new System.Exception("Failed to build Azure AD Application. (AAD disabled)");
            }
        }
    }
}
