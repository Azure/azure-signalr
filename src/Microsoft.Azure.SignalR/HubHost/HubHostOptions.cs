// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.Azure.SignalR
{
    public class HubHostOptions
    {
        public static readonly int DefaultConnectionNumber = 5;
        public static readonly ProtocolType DefaultProtocolType = ProtocolType.Text;

        public int ConnectionNumber { get; set; } = DefaultConnectionNumber;

        public ProtocolType ProtocolType { get; set; } = DefaultProtocolType;
    }
}
