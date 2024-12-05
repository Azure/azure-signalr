// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Microsoft.Azure.SignalR.Controllers.Common
{
    [Authorize]
    [Route("api/v1")]
    [Route("api")]
    [Consumes("application/json")]
    internal abstract class SignalRServiceWebApiDefinition : ControllerBase
    {
        public const string ApplicationName = "application";
        public const string ExcludedName = "excluded";
        public const string ReasonName = "reason";
        public const string TtlName = "ttl";

        /// <summary>
        /// Get service health status.
        /// </summary>
        /// <response code="200">The service is healthy</response>
        [HttpGet("health"), HttpHead("health")]
        [ProducesResponseType(200)]
        [AllowAnonymous]
        public abstract IActionResult GetHealthStatus();

        // POST /api/v1/hubs/chat
        /// <summary>
        /// Broadcast a message to all clients connected to target hub.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="message">The message body.</param>
        /// <param name="excluded">Excluded connection Ids</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}")]
        [HttpPost("hubs/{hub}/:send")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> Broadcast(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message, 
            [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);


        // POST /api/v1/hubs/chat/users/1
        /// <summary>
        /// Broadcast a message to all clients belong to the target user.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">The user Id.</param>
        /// <param name="message">The message body.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/users/{user}")]
        [HttpPost("hubs/{hub}/users/{user}/:send")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> SendToUser(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string user,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // POST /api/v1/hubs/chat/connections/123-456
        /// <summary>
        /// Send message to the specific connection.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="message">The message body.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/connections/{connectionId}")]
        [HttpPost("hubs/{hub}/connections/{connectionId}/:send")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> SendToConnection(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string connectionId,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // POST /api/v1/hubs/chat/groups/1
        /// <summary>
        /// Broadcast a message to all clients within the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="message">The message body.</param>
        /// <param name="excluded">Excluded connection Ids</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/groups/{group}")]
        [HttpPost("hubs/{hub}/groups/{group}/:send")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> GroupBroadcast(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
            [Required(ErrorMessage = ErrorMessages.Validation.MessageRequired)]
            PayloadMessage message, [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);


        #region Connections
        // POST /api/hubs/chat/:closeConnections
        /// <summary>
        /// Close all of the connections in the hub.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="excluded">Exclude these connectionIds when closing the connections in the hub.</param>
        /// <param name="reason">The reason closing the client connections.</param>
        /// <returns></returns>
        [ProducesResponseType(204)]
        [HttpPost("hubs/{hub}/:closeConnections")]
        public abstract Task<IActionResult> CloseConnections(
           [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
           [FromQuery(Name = ApplicationName)]
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null,
           [FromQuery(Name = ExcludedName)]
            IReadOnlyList<string> excluded = null,
           [FromQuery(Name = ReasonName)]
            string reason = null);

        // POST /api/hubs/chat/groups/g123/:closeConnections
        /// <summary>
        /// Close connections in the specific group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="excluded">Exclude these connectionIds when closing the connections in the hub.</param>
        /// <param name="reason">The reason closing the client connections.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/groups/{group}/:closeConnections")]
        [ProducesResponseType(204)]
        public abstract Task<IActionResult> CloseGroupConnections(
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
            string reason = null);

        // POST /api/hubs/chat/users/u123/:closeConnections
        /// <summary>
        /// Close connections for the specific user.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">The user Id.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="excluded">Exclude these connectionIds when closing the connections in the hub.</param>
        /// <param name="reason">The reason closing the client connections.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/users/{user}/:closeConnections")]
        [ProducesResponseType(204)]
        public abstract Task<IActionResult> CloseUserConnections(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1)]
            string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null,
            [FromQuery(Name = ExcludedName)]
            IReadOnlyList<string> excluded = null,
            [FromQuery(Name = ReasonName)]
            string reason = null);

        // DELETE /api/hubs/chat/connections/c123/groups
        /// <summary>
        /// Remove a connection from all groups
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">Target connection Id</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/connections/{connectionId}/groups")]
        [ProducesResponseType(200)]
        public abstract IActionResult RemoveConnectionFromAllGroups(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)] string hub,
            [MinLength(1, ErrorMessage = ErrorMessages.Validation.InvalidConnectionId)] string connectionId,
            [FromQuery(Name = ApplicationName), RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)] string application = null);


        // GET .../chat/connections/123-456
        // HEAD .../chat/connections/123-456
        /// <summary>
        /// Check if the connection with the given connectionId exists
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/connections/{connectionId}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract IActionResult CheckConnectionExistence(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string connectionId,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // GET .../chat/groups/group1
        // HEAD .../chat/groups/group1
        /// <summary>
        /// Check if there are any client connections inside the given group
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/groups/{group}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract IActionResult CheckGroupExistence(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);


        // GET .../chat/users/user1
        // HEAD .../chat/users/user1
        /// <summary>
        /// Check if there are any client connections connected for the given user
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">The user Id.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/users/{user}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract IActionResult CheckUserExistence(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // DELETE .../chat/connections/a?reason=reason
        /// <summary>
        /// Close the client connection
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="reason">The reason of the connection close.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/connections/{connectionId}")]
        [ProducesResponseType(200)]
        public abstract Task<IActionResult> CloseClientConnection(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string connectionId, [FromQuery] string reason,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        #endregion

        #region Connection-Group

        // PUT .../chat/groups/1/connections/123-456
        // PUT .../chat/connections/123-456/groups/1
        /// <summary>
        /// Add a connection to the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="connectionId">Target connection Id</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPut("hubs/{hub}/groups/{group}/connections/{connectionId}")]
        [HttpPut("hubs/{hub}/connections/{connectionId}/groups/{group}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract IActionResult AddConnectionToGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string connectionId,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // DELETE .../chat/groups/1/connections/a
        // DELETE .../chat/connections/a/groups/1
        /// <summary>
        /// Remove a connection from the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="connectionId">Target connection Id</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/groups/{group}/connections/{connectionId}")]
        [HttpDelete("hubs/{hub}/connections/{connectionId}/groups/{group}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract IActionResult RemoveConnectionFromGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string connectionId,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        #endregion

        #region User-Group

        // GET .../chat/groups/1/users/a
        // GET .../chat/users/a/groups/1
        /// <summary>
        /// Check whether a user exists in the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="user">Target user Id</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/groups/{group}/users/{user}")]
        [Route("hubs/{hub}/users/{user}/groups/{group}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract IActionResult CheckUserExistenceInGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // PUT .../chat/groups/1/users/a?ttl=100
        // PUT .../chat/users/a/groups/1?ttl=100
        /// <summary>
        /// Add a user to the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="user">Target user Id</param>
        /// <param name="ttl">Specifies the seconds that the user exists in the group. If not set, the user lives in the group forever.</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpPut("hubs/{hub}/groups/{group}/users/{user}")]
        [HttpPut("hubs/{hub}/users/{user}/groups/{group}")]
        [ProducesResponseType(202)]
        public abstract IActionResult AddUserToGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string user, int? ttl = null,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // DELETE .../chat/groups/1/users/a
        // DELETE .../chat/users/a/groups/1
        /// <summary>
        /// Remove a user from the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="user">Target user Id</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/groups/{group}/users/{user}")]
        [HttpDelete("hubs/{hub}/users/{user}/groups/{group}")]
        [ProducesResponseType(202)]
        public abstract IActionResult RemoveUserFromGroup(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub,
            [StringLength(1024, MinimumLength = 1, ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            [RegularExpression(ParameterValidator.NotWhitespacePattern , ErrorMessage = ErrorMessages.Validation.InvalidGroupName)]
            string group, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        // DELETE .../chat/users/a/groups
        /// <summary>
        /// Remove a user from all groups.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">Target user Id</param>
        /// <param name="application">Target application name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <returns></returns>
        /// <response code="200">The user is deleted</response>
        /// <response code="202">The delete request is accepted and service is handling the request int the background</response>
        [HttpDelete("hubs/{hub}/users/{user}/groups")]
        [ProducesResponseType(200)]
        [ProducesResponseType(202)]
        public abstract IActionResult RemoveUserFromAllGroups(
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidHubNameInPath)]
            string hub, string user,
            [RegularExpression(ParameterValidator.HubNamePattern, ErrorMessage = ErrorMessages.Validation.InvalidApplicationName)]
            string application = null);

        #endregion
    }
}
