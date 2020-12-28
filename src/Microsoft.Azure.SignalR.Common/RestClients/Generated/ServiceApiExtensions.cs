// <auto-generated>
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.
//
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Microsoft.Azure.SignalR
{
    using Models;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extension methods for ServiceApi.
    /// </summary>
    public static partial class ServiceApiExtensions
    {
            /// <summary>
            /// Broadcast a message to all clients connected to target hub.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='payloadMessage'>
            /// The message body.
            /// </param>
            /// <param name='excluded'>
            /// Excluded connection Ids
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task BroadcastAsync(this IServiceApi operations, string hub, PayloadMessage payloadMessage, IList<string> excluded = default(IList<string>), CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.BroadcastWithHttpMessagesAsync(hub, payloadMessage, excluded, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Check if there are any client connections connected for the given user
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='user'>
            /// The user Id.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task CheckUserExistenceAsync(this IServiceApi operations, string hub, string user, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.CheckUserExistenceWithHttpMessagesAsync(hub, user, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Broadcast a message to all clients belong to the target user.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='user'>
            /// The user Id.
            /// </param>
            /// <param name='payloadMessage'>
            /// The message body.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task SendToUserAsync(this IServiceApi operations, string hub, string user, PayloadMessage payloadMessage, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.SendToUserWithHttpMessagesAsync(hub, user, payloadMessage, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Check if the connection with the given connectionId exists
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='connectionId'>
            /// The connection Id.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task CheckConnectionExistenceAsync(this IServiceApi operations, string hub, string connectionId, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.CheckConnectionExistenceWithHttpMessagesAsync(hub, connectionId, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Close the client connection
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='connectionId'>
            /// The connection Id.
            /// </param>
            /// <param name='reason'>
            /// The reason of the connection close.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task CloseClientConnectionAsync(this IServiceApi operations, string hub, string connectionId, string reason = default(string), CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.CloseClientConnectionWithHttpMessagesAsync(hub, connectionId, reason, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Send message to the specific connection.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='connectionId'>
            /// The connection Id.
            /// </param>
            /// <param name='payloadMessage'>
            /// The message body.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task SendToConnectionAsync(this IServiceApi operations, string hub, string connectionId, PayloadMessage payloadMessage, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.SendToConnectionWithHttpMessagesAsync(hub, connectionId, payloadMessage, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Check if there are any client connections inside the given group
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task CheckGroupExistenceAsync(this IServiceApi operations, string hub, string group, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.CheckGroupExistenceWithHttpMessagesAsync(hub, group, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Broadcast a message to all clients within the target group.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='payloadMessage'>
            /// The message body.
            /// </param>
            /// <param name='excluded'>
            /// Excluded connection Ids
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task GroupBroadcastAsync(this IServiceApi operations, string hub, string group, PayloadMessage payloadMessage, IList<string> excluded = default(IList<string>), CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.GroupBroadcastWithHttpMessagesAsync(hub, group, payloadMessage, excluded, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Add a connection to the target group.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='connectionId'>
            /// Target connection Id
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task AddConnectionToGroupAsync(this IServiceApi operations, string hub, string group, string connectionId, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.AddConnectionToGroupWithHttpMessagesAsync(hub, group, connectionId, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Remove a connection from the target group.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='connectionId'>
            /// Target connection Id
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task RemoveConnectionFromGroupAsync(this IServiceApi operations, string hub, string group, string connectionId, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.RemoveConnectionFromGroupWithHttpMessagesAsync(hub, group, connectionId, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Check whether a user exists in the target group.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='user'>
            /// Target user Id
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task CheckUserExistenceInGroupAsync(this IServiceApi operations, string hub, string group, string user, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.CheckUserExistenceInGroupWithHttpMessagesAsync(hub, group, user, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Add a user to the target group.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='user'>
            /// Target user Id
            /// </param>
            /// <param name='ttl'>
            /// Specifies the seconds that the user exists in the group. If not set, the
            /// user lives in the group forever.
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task AddUserToGroupAsync(this IServiceApi operations, string hub, string group, string user, int? ttl = default(int?), CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.AddUserToGroupWithHttpMessagesAsync(hub, group, user, ttl, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Remove a user from the target group.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='group'>
            /// Target group name, which length should be greater than 0 and less than
            /// 1025.
            /// </param>
            /// <param name='user'>
            /// Target user Id
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task RemoveUserFromGroupAsync(this IServiceApi operations, string hub, string group, string user, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.RemoveUserFromGroupWithHttpMessagesAsync(hub, group, user, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

            /// <summary>
            /// Remove a user from all groups.
            /// </summary>
            /// <param name='operations'>
            /// The operations group for this extension method.
            /// </param>
            /// <param name='hub'>
            /// Target hub name, which should start with alphabetic characters and only
            /// contain alpha-numeric characters or underscore.
            /// </param>
            /// <param name='user'>
            /// Target user Id
            /// </param>
            /// <param name='cancellationToken'>
            /// The cancellation token.
            /// </param>
            public static async Task RemoveUserFromAllGroupsAsync(this IServiceApi operations, string hub, string user, CancellationToken cancellationToken = default(CancellationToken))
            {
                (await operations.RemoveUserFromAllGroupsWithHttpMessagesAsync(hub, user, null, cancellationToken).ConfigureAwait(false)).Dispose();
            }

    }
}
