// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Controllers.Common;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Emulator.Controllers
{
    internal class SignalRServiceEmulatorWebApi : SignalRServiceWebApiDefinition
    {
        private readonly DynamicHubContextStore _store;
        private readonly ILogger<SignalRServiceEmulatorWebApi> _logger;

        public SignalRServiceEmulatorWebApi(DynamicHubContextStore store, ILogger<SignalRServiceEmulatorWebApi> _logger) : base()
        {
            _store = store;
            this._logger = _logger;
        }

        public override async Task<IActionResult> Broadcast([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, [FromBody] PayloadMessage message, [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);

                await SendAsync(clients.AllExcept(excluded), message.Target, arguments);
            }

            return Accepted();
        }

        public override async Task<IActionResult> SendToUser([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string user, [FromBody] PayloadMessage message)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);

                await SendAsync(clients.User(user), message.Target, arguments);
            }

            return Accepted();
        }

        public override Task<IActionResult> CheckConnectionExistence([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string connectionId)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var lifetime = c.LifetimeManager;
                var connection = lifetime.Connections[connectionId];
                if (connection != null)
                {
                    return Task.FromResult(Ok() as IActionResult);
                }
            }

            return Task.FromResult(NotFound() as IActionResult);
        }

        public override Task<IActionResult> CheckGroupExistence([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                if (c.UserGroupManager.GroupContainsConnections(group))
                {
                    return Task.FromResult(Ok() as IActionResult);
                }
            }

            return Task.FromResult(NotFound() as IActionResult);
        }

        public override Task<IActionResult> CheckUserExistence([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string user)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                foreach (var conn in c.LifetimeManager.Connections)
                {
                    if (string.Equals(conn.UserIdentifier, user, StringComparison.Ordinal))
                    {
                        return Task.FromResult(Ok() as IActionResult);
                    }
                }
            }

            return Task.FromResult(NotFound() as IActionResult);
        }

        public override Task<IActionResult> CloseClientConnection([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string connectionId, [FromQuery] string reason)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var lifetime = c.LifetimeManager;
                var connection = lifetime.Connections[connectionId];
                if (connection != null)
                {
                    connection.Abort();
                }
            }

            return Task.FromResult(Accepted() as IActionResult);
        }

        public override Task<IActionResult> RemoveConnectionFromGroup([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group, string connectionId)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                if (c.UserGroupManager.RemoveConnectionFromGroup(connectionId, group))
                {
                    return Task.FromResult(Ok() as IActionResult);
                }
            }

            return Task.FromResult(NotFound() as IActionResult);
        }

        public override Task<IActionResult> AddConnectionToGroup([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group, string connectionId)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var lifetime = c.LifetimeManager;
                var connection = lifetime.Connections[connectionId];
                if (connection != null)
                {
                    c.UserGroupManager.AddConnectionIntoGroup(connectionId, group);
                    return Task.FromResult(Ok() as IActionResult);
                }
            }

            return Task.FromResult(NotFound() as IActionResult);
        }

        public override async Task<IActionResult> GroupBroadcast([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group, [FromBody] PayloadMessage message, [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);
                using (var lease = c.UserGroupManager.GetConnectionsForGroup(group))
                {
                    if (lease.Value.Count > 0)
                    {
                        await SendAsync(clients.Clients(lease.Value.Except(excluded).ToArray()), message.Target, arguments);
                    }
                }
            }

            return Accepted();
        }

        public override async Task<IActionResult> SendToConnection([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string connectionId, [FromBody] PayloadMessage message)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);

                await SendAsync(clients.Client(connectionId), message.Target, arguments);
            }

            return Accepted();
        }

        public override Task<IActionResult> CheckUserExistenceInGroup([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group, string user)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                if (c.UserGroupManager.GroupContainsUser(user, group))
                {
                    return Task.FromResult(Ok() as IActionResult);
                }
            }

            return Task.FromResult(NotFound() as IActionResult);
        }

        public override Task<IActionResult> AddUserToGroup([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group, string user, int? ttl = null)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                c.UserGroupManager.AddUserToGroup(user, group, ttl == null ? DateTimeOffset.MaxValue : DateTimeOffset.Now.AddSeconds(ttl.Value));
            }

            return Task.FromResult(Accepted() as IActionResult);
        }

        public override Task<IActionResult> RemoveUserFromAllGroups([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string user)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                c.UserGroupManager.RemoveUserFromAllGroups(user);
                return Task.FromResult(Ok() as IActionResult);
            }

            return Task.FromResult(Accepted() as IActionResult);
        }

        public override Task<IActionResult> RemoveUserFromGroup([RegularExpression("^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$")] string hub, string group, string user)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult(BadRequest() as IActionResult);
            }

            if (_store.TryGetLifetimeContext(hub, out var c))
            {
                c.UserGroupManager.RemoveUserFromGroup(user, group);
            }

            return Task.FromResult(Accepted() as IActionResult);
        }

        public override Task<IActionResult> GetHealthStatus()
        {
            return Task.FromResult(Ok() as IActionResult);
        }

        private Task SendAsync(IClientProxy client, string method, object[] arguments, CancellationToken cancellationToken = default)
        {
            switch (arguments.Length)
            {
                case 0:
                    return client.SendAsync(method, cancellationToken);
                case 1:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        cancellationToken);
                case 2:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        cancellationToken);
                case 3:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        cancellationToken);
                case 4:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        cancellationToken);
                case 5:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        cancellationToken);
                case 6:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        arguments[5],
                        cancellationToken);
                case 7:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        arguments[5],
                        arguments[6],
                        cancellationToken);
                case 8:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        arguments[5],
                        arguments[6],
                        arguments[7],
                        cancellationToken);
                case 9:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        arguments[5],
                        arguments[6],
                        arguments[7],
                        arguments[8],
                        cancellationToken);
                case 10:
                    return client.SendAsync(
                        method,
                        arguments[0],
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        arguments[5],
                        arguments[6],
                        arguments[7],
                        arguments[8],
                        arguments[9],
                        cancellationToken);
                default:
                    throw new NotSupportedException();
            }
        }

        private object[] SafeConvertToObjectArray(PayloadMessage payload)
        {
            try
            {
                return JsonObjectConverter.ConvertToObjectArray(payload.Arguments);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to parse argument", ex);
                return null;
            }
        }
    }
}
