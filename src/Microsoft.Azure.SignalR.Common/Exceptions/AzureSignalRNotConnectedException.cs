// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    public class AzureSignalRNotConnectedException : AzureSignalRException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRNotConnectedException"/> class.
        /// </summary>
        public AzureSignalRNotConnectedException() : base("Azure SignalR Service is not connected yet, please try again later.")
        {
        }
    }
}
