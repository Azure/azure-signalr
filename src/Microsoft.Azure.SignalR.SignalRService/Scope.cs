// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.SignalRService
{
    public enum Scope
    {
        Client, // scope for  client to connect to a hub
        Server, // scope for all server side operations

        // compatible with current runtime's scope
        Broadcast,
        SendToUser,
        SendToClient,
        SendToGroup,
        AddUserToGroup,
        RemoveUserFromGroup,
        AddConnectionToGroup,
        RemoveConnectionFromGroup
    }
}
