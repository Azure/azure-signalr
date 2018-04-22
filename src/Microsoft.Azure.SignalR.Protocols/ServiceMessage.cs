// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class ServiceMessage
    {
    }

    public class HandshakeRequestMessage : ServiceMessage
    {
        public int Version { get; set; }

        public HandshakeRequestMessage(int version)
        {
            Version = version;
        }
    }

    public class HandshakeResponseMessage : ServiceMessage
    {
        public string ErrorMessage { get; set; }

        public HandshakeResponseMessage(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }

    public class PingMessage : ServiceMessage
    {
        public static PingMessage Instance = new PingMessage();
    }
}
