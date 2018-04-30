// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public class ProtocolVersionNotSupportedException : AzureSignalRException
    {
        public ProtocolVersionNotSupportedException(int protocolVersion)
            : base($"Protocol version {protocolVersion} not supported by service. Use the right version of SDK with matching protocol version of the service.")
        {
        }
    }
}
