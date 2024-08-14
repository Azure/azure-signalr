// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Controllers.Common;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Emulator.Controllers
{
    [ApiController]
    internal class SignalRServiceEmulatorWebApi : SignalRServiceWebApiDefinition
    {
        private const string HubPattern = "^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$";
        private const string GroupPattern = "^\\S{1,1024}$";
        private readonly IDynamicHubContextStore _store;
        private readonly ILogger<SignalRServiceEmulatorWebApi> _logger;

        public SignalRServiceEmulatorWebApi(IDynamicHubContextStore store, ILogger<SignalRServiceEmulatorWebApi> _logger) : base()
        {
            _store = store;
            this._logger = _logger;
        }

        public override async Task<IActionResult> Broadcast(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message,
            [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);

                await SendAsync(clients.AllExcept(excluded), message.Target, arguments);
            }

            return Accepted();
        }

        public override async Task<IActionResult> SendToUser(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string user,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);

                await SendAsync(clients.User(user), message.Target, arguments);
            }

            return Accepted();
        }

        public override IActionResult CheckConnectionExistence(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string connectionId,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var lifetime = c.LifetimeManager;
                var connection = lifetime.Connections[connectionId];
                if (connection != null)
                {
                    return Ok();
                }
            }

            Response.SetMsErrorCodeHeader(Constants.ErrorCodes.Warning.ConnectionNotExisted);

            return NotFound();
        }

        public override IActionResult CheckGroupExistence(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                if (c.UserGroupManager.GroupContainsConnections(group))
                {
                    return Ok();
                }
            }

            Response.SetMsErrorCodeHeader(Constants.ErrorCodes.Warning.GroupNotExisted);

            return NotFound();
        }

        public override IActionResult CheckUserExistence(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                foreach (var conn in c.LifetimeManager.Connections)
                {
                    if (string.Equals(conn.UserIdentifier, user, StringComparison.Ordinal))
                    {
                        return Ok();
                    }
                }
            }

            Response.SetMsErrorCodeHeader(Constants.ErrorCodes.Warning.UserNotExisted);

            return NotFound();
        }

        public override async Task<IActionResult> CloseClientConnection(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string connectionId, [FromQuery] string reason,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var lifetime = c.LifetimeManager;
                var connection = lifetime.Connections[connectionId];
                if (connection != null)
                {
                    await SendCloseAsync(connection, reason);
                }
            }

            return Ok();
        }

        public override IActionResult RemoveConnectionFromGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string connectionId,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                if (c.UserGroupManager.RemoveConnectionFromGroup(connectionId, group))
                {
                    return Ok();
                }
            }

            Response.SetMsErrorCodeHeader(Constants.ErrorCodes.Error.ConnectionNotExisted);

            return NotFound();
        }

        public override IActionResult RemoveConnectionFromAllGroups(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)] string hub,
            [MinLength(1, ErrorMessage = ErrorMessages.Validation.InvalidConnectionId)] string connectionId,
            [FromQuery(Name = ApplicationName), RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)] string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                c.UserGroupManager.RemoveConnectionFromAllGroups(connectionId);
                return Ok();
            }

            return Ok();
        }

        public override IActionResult AddConnectionToGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string connectionId,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var lifetime = c.LifetimeManager;
                var connection = lifetime.Connections[connectionId];
                if (connection != null)
                {
                    c.UserGroupManager.AddConnectionIntoGroup(connectionId, group);
                    return Ok();
                }
            }

            Response.SetMsErrorCodeHeader(Constants.ErrorCodes.Error.ConnectionNotExisted);

            return NotFound();
        }

        public override async Task<IActionResult> GroupBroadcast(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message,
            [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
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

        public override async Task<IActionResult> SendToConnection(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string connectionId,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var clients = c.ClientManager;
                var arguments = SafeConvertToObjectArray(message);

                await SendAsync(clients.Client(connectionId), message.Target, arguments);
            }

            return Accepted();
        }

        public override IActionResult CheckUserExistenceInGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                if (c.UserGroupManager.GroupContainsUser(group, user))
                {
                    return Ok();
                }
            }

            Response.SetMsErrorCodeHeader(Constants.ErrorCodes.Info.UserNotInGroup);

            return NotFound();
        }

        public override IActionResult AddUserToGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string user, int? ttl = null,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                c.UserGroupManager.AddUserToGroup(user, group, ttl == null ? DateTimeOffset.MaxValue : DateTimeOffset.Now.AddSeconds(ttl.Value));
            }

            return Accepted();
        }

        public override IActionResult RemoveUserFromAllGroups(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                c.UserGroupManager.RemoveUserFromAllGroups(user);
            }

            return Ok();
        }

        public override IActionResult RemoveUserFromGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                c.UserGroupManager.RemoveUserFromGroup(user, group);
            }

            return Accepted();
        }

        public override async Task<IActionResult> CloseConnections(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [FromQuery(Name = ApplicationName)]
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null,
            [FromQuery(Name = ExcludedName)]
            IReadOnlyList<string> excluded = null,
            [FromQuery(Name = ReasonName)]
            string reason = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                var lifetime = c.LifetimeManager;
                foreach (var cc in lifetime.Connections)
                {
                    if (cc != null)
                    {
                        await SendCloseAsync(cc, reason);
                    }
                }
            }

            return NoContent();
        }

        public override async Task<IActionResult> CloseGroupConnections(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group,
            [FromQuery(Name = ApplicationName)]
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null,
            [FromQuery(Name = ExcludedName)]
            IReadOnlyList<string> excluded = null,
            [FromQuery(Name = ReasonName)]
            string reason = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                foreach (var cc in c.UserGroupManager.GetConnectionsForGroup(group).Value)
                {
                    var lifetime = c.LifetimeManager;
                    var connection = lifetime.Connections[cc];

                    if (connection != null)
                    {
                        await SendCloseAsync(connection, reason);
                    }
                }
            }

            return NoContent();
        }

        public override async Task<IActionResult> CloseUserConnections(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1)]
            string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null,
            [FromQuery(Name = ExcludedName)]
            IReadOnlyList<string> excluded = null,
            [FromQuery(Name = ReasonName)]
            string reason = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (_store.TryGetLifetimeContext(GetInternalHubName(application, hub), out var c))
            {
                foreach (var cc in c.UserGroupManager.GetConnectionsForUser(user).Value)
                {
                    if (cc != null)
                    {
                        await SendCloseAsync(cc, reason);
                    }
                }
            }

            return NoContent();
        }

        public override IActionResult GetHealthStatus()
        {
            return Ok();
        }

        private Task SendAsync(IClientProxy client, string method, object[] arguments, CancellationToken cancellationToken = default)
        {
            var argsLen = arguments?.Length ?? 0;

            switch (argsLen)
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
                _logger.LogError("Failed to parse argument: {0}", ex);
                return null;
            }
        }

        private readonly ConcurrentDictionary<(string hub, string application), string> _hubs = new();

        private string GetInternalHubName(string application, string hub)
        {
            if (_hubs.TryGetValue((hub, application), out var result))
            {
                return result;
            }
            if (string.IsNullOrEmpty(application))
            {
                return _hubs.GetOrAdd((hub, application), hub.ToLower());
            }
            return _hubs.GetOrAdd((hub, application), application.ToLower() + "_" + hub.ToLower());
        }

        private ValueTask SendCloseAsync(HubConnectionContext connection, string message)
        {
            return connection.WriteAsync(new CloseMessage(message, true));
        }
    }
}
