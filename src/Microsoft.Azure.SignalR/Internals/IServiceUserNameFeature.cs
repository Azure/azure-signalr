// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// When clients negotiate with Management SDK and connect to SignalR server, the <see cref="IUserIdProvider"/> might not work as the user Id is set directly in the Management SDK.
    /// To make <see cref="HubConnectionContext.UserIdentifier"/> have the valid value in this case, we should set it before the server can access it. <see cref="HubLifetimeManager{THub}.OnConnectedAsync(HubConnectionContext)"/> is the only chance we can set the value. However, we cannot access the <see cref="Constants.ClaimType.UserId"/> as ASRS system claims're trimmed there. <see cref="HubConnectionContext.Features"/> is the place where we can store the user Id.
    /// https://github.com/dotnet/aspnetcore/blob/v6.0.9/src/SignalR/server/Core/src/HubConnectionHandler.cs#L132-L141
    /// </summary>
    internal interface IServiceUserIdFeature
    {
        string UserId { get; }
    }
}
