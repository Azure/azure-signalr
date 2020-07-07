// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Identity.Client;

namespace Microsoft.Azure.SignalR
{
    internal class AzureActiveDirectoryHelper
    {
        public static IConfidentialClientApplication BuildApplication(AzureActiveDirectoryOptions options)
        {
            if (options == null)
            {
                throw new InvalidOperationException("Failed to build Azure Active Directory application. (disabled)");
            }

            var builder = ConfidentialClientApplicationBuilder.Create(options.ClientId)
                .WithAuthority(options.BuildAuthority());
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
    }
}