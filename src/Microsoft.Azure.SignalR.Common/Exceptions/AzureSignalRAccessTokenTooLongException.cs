// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common
{
    /// <summary>
    /// The exception thrown when AccessToken is too long.
    /// </summary>
    public class AzureSignalRAccessTokenTooLongException : AzureSignalRException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRAccessTokenTooLongException"/> class.
        /// </summary>
        public AzureSignalRAccessTokenTooLongException() : base($"AccessToken must not be longer than 4K.")
        {
        }
    }
}
