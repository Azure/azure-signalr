// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Azure.Core;

namespace Microsoft.Azure.SignalR.Common
{
    /// <summary>
    /// The exception throws when AccessKey is not authorized.
    /// </summary>
    public class AzureSignalRAccessTokenNotAuthorizedException : AzureSignalRException
    {
        private const string Postfix = " appears to lack the permission to generate access tokens, see innerException for more details.";

        [Obsolete("use (TokenCredential, Exception) instead.")]
        public AzureSignalRAccessTokenNotAuthorizedException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRAccessTokenNotAuthorizedException"/> class.
        /// </summary>
        public AzureSignalRAccessTokenNotAuthorizedException(string credentialName, Exception innerException) :
            base(credentialName + Postfix, innerException)
        {
        }
    }
}
