// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common
{
    public class AzureSignalRConfigurationNoEndpointException : AzureSignalRException
    {
        public AzureSignalRConfigurationNoEndpointException() : base("No connection string was specified.")
        {
        }
    }
}
