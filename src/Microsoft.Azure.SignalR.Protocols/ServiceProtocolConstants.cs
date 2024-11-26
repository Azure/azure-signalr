// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

namespace Microsoft.Azure.SignalR.Protocol;

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
    public const int UserJoinGroupWithAckMessageType = 26;
    public const int UserLeaveGroupWithAckMessageType = 27;
    public const int AccessKeyRequestType = 28;
    public const int AccessKeyResponseType = 29;
    public const int CloseConnectionWithAckMessageType = 30;
    public const int CloseConnectionsWithAckMessageType = 31;
    public const int CloseUserConnectionsWithAckMessageType = 32;
    public const int CloseGroupConnectionsWithAckMessageType = 33;
    public const int ClientInvocationMessageType = 34;
    public const int ClientCompletionMessageType = 35;
    public const int ErrorCompletionMessageType = 36;
    public const int ServiceMappingMessageType = 37;
    public const int ConnectionReconnectMessageType = 38;
    public const int ConnectionFlowControlMessageType = 39;
}
