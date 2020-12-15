// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common
{
    /// <summary>
    /// The exception throws when AccessKey is not authorized.
    /// </summary>
    public class AzureSignalRAccessTokenNotAuthorizedException : AzureSignalRException
    {
         /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRAccessTokenNotAuthorizedException"/> class.
        /// </summary>
        public AzureSignalRAccessTokenNotAuthorizedException() : base("This AccessKey doesn't have the permission to generate access token.")
        {
        }
    }
}
