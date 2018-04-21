// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    public static class ProtocolConstants
    {
        public const int PingMessageType = 1;
        public const int OpenConnectionMessageType = 2;
        public const int CloseConnectionMessageType = 3;
        public const int ConnectionDataMessageType = 4;
        public const int MultiConnectionDataMessageType = 5;
        public const int UserDataMessageType = 6;
        public const int MultiUserDataMessageType = 7;
        public const int BroadcastDataMessageType = 8;
        public const int JoinGroupMessageType = 9;
        public const int LeaveGroupMessageType = 10;
        public const int GroupBroadcastDataMessageType = 11;
        public const int MultiGroupBroadcastDataMessageType = 12;
    }
}
