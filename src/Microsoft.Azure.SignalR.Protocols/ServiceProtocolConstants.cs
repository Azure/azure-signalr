// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    public static class ServiceProtocolConstants
    {
        public const int HandshakeRequestType = 1;
        public const int HandshakeResponseType = 2;
        public const int PingMessageType = 3;
        public const int OpenConnectionMessageType = 4;
        public const int CloseConnectionMessageType = 5;
        public const int ConnectionDataMessageType = 6;
        public const int MultiConnectionDataMessageType = 7;
        public const int UserDataMessageType = 8;
        public const int MultiUserDataMessageType = 9;
        public const int BroadcastDataMessageType = 10;
        public const int JoinGroupMessageType = 11;
        public const int LeaveGroupMessageType = 12;
        public const int GroupBroadcastDataMessageType = 13;
        public const int MultiGroupBroadcastDataMessageType = 14;
        public const int ServiceErrorMessageType = 15;
        public const int UserJoinGroupMessageType = 16;
        public const int UserLeaveGroupMessageType = 17;
        public const int JoinGroupWithAckMessageType = 18;
        public const int LeaveGroupWithAckMessageType = 19;
        public const int AckMessageType = 20;
        public const int CheckUserInGroupWithAckMessageType = 21;
        public const int ServiceEventMessageType = 22;
        public const int CheckGroupExistenceWithAckMessageType = 23;
        public const int CheckConnectionExistenceWithAckMessageType = 24;
        public const int CheckUserExistenceWithAckMessageType = 25;
    }
}
