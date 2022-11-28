// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Azure.SignalR.Controllers.Common
{
    [Authorize]
    [Route("api/v1")]
    [Route("api")]
    [Consumes("application/json")]
    internal abstract class SignalRServiceWebApiDefinition : ControllerBase
    {
        /// <summary>
        /// Get service health status.
        /// </summary>
        /// <response code="200">The service is healthy</response>
        [HttpGet("health"), HttpHead("health")]
        [ProducesResponseType(200)]
        public abstract Task<IActionResult> GetHealthStatus();

        // POST /api/v1/hubs/chat
        /// <summary>
        /// Broadcast a message to all clients connected to target hub.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="message">The message body.</param>
        /// <param name="excluded">Excluded connection Ids</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> Broadcast(string hub, [FromBody] PayloadMessage message, [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded);


        // POST /api/v1/hubs/chat/users/1
        /// <summary>
        /// Broadcast a message to all clients belong to the target user.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">The user Id.</param>
        /// <param name="message">The message body.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/users/{user}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> SendToUser(string hub, string user, [FromBody] PayloadMessage message);

        // POST /api/v1/hubs/chat/connections/123-456
        /// <summary>
        /// Send message to the specific connection.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="message">The message body.</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/connections/{connectionId}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> SendToConnection(string hub, string connectionId, [FromBody] PayloadMessage message);

        // POST /api/v1/hubs/chat/groups/1
        /// <summary>
        /// Broadcast a message to all clients within the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="message">The message body.</param>
        /// <param name="excluded">Excluded connection Ids</param>
        /// <returns></returns>
        [HttpPost("hubs/{hub}/groups/{group}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> GroupBroadcast(string hub, string group, [FromBody] PayloadMessage message, [FromQuery(Name = "excluded")] IReadOnlyList<string> excluded);


        #region Connections

        // GET .../chat/connections/123-456
        // HEAD .../chat/connections/123-456
        /// <summary>
        /// Check if the connection with the given connectionId exists
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/connections/{connectionId}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> CheckConnectionExistence(string hub, string connectionId);

        // GET .../chat/groups/group1
        // HEAD .../chat/groups/group1
        /// <summary>
        /// Check if there are any client connections inside the given group
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/groups/{group}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> CheckGroupExistence(string hub, string group);


        // GET .../chat/users/user1
        // HEAD .../chat/users/user1
        /// <summary>
        /// Check if there are any client connections connected for the given user
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">The user Id.</param>
        /// <returns></returns>
        [Route("hubs/{hub}/users/{user}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> CheckUserExistence(string hub, string user);

        // DELETE .../chat/connections/a?reason=reason
        /// <summary>
        /// Close the client connection
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="reason">The reason of the connection close.</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/connections/{connectionId}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> CloseClientConnection(string hub, string connectionId, [FromQuery] string reason);

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
        /// <returns></returns>
        [HttpPut("hubs/{hub}/groups/{group}/connections/{connectionId}")]
        [HttpPut("hubs/{hub}/connections/{connectionId}/groups/{group}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> AddConnectionToGroup(string hub, string group, string connectionId);

        // DELETE .../chat/groups/1/connections/a
        // DELETE .../chat/connections/a/groups/1
        /// <summary>
        /// Remove a connection from the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="connectionId">Target connection Id</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/groups/{group}/connections/{connectionId}")]
        [HttpDelete("hubs/{hub}/connections/{connectionId}/groups/{group}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> RemoveConnectionFromGroup(string hub, string group, string connectionId);

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
        /// <returns></returns>
        [Route("hubs/{hub}/groups/{group}/users/{user}")]
        [Route("hubs/{hub}/users/{user}/groups/{group}")]
        [HttpGet, HttpHead]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> CheckUserExistenceInGroup(string hub, string group, string user);

        // PUT .../chat/groups/1/users/a?ttl=100
        // PUT .../chat/users/a/groups/1?ttl=100
        /// <summary>
        /// Add a user to the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="user">Target user Id</param>
        /// <param name="ttl">Specifies the seconds that the user exists in the group. If not set, the user lives in the group forever.</param>
        /// <returns></returns>
        [HttpPut("hubs/{hub}/groups/{group}/users/{user}")]
        [HttpPut("hubs/{hub}/users/{user}/groups/{group}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> AddUserToGroup(string hub, string group, string user, int? ttl = null);

        // DELETE .../chat/groups/1/users/a
        // DELETE .../chat/users/a/groups/1
        /// <summary>
        /// Remove a user from the target group.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="group">Target group name, which length should be greater than 0 and less than 1025.</param>
        /// <param name="user">Target user Id</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/groups/{group}/users/{user}")]
        [HttpDelete("hubs/{hub}/users/{user}/groups/{group}")]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> RemoveUserFromGroup(string hub, string group, string user);

        // DELETE .../chat/users/a/groups
        /// <summary>
        /// Remove a user from all groups.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="user">Target user Id</param>
        /// <returns></returns>
        /// <response code="200">The user is deleted</response>
        /// <response code="202">The delete request is accepted and service is handling the request int the background</response>
        [HttpDelete("hubs/{hub}/users/{user}/groups")]
        [ProducesResponseType(200)]
        [ProducesResponseType(202)]
        public abstract Task<IActionResult> RemoveUserFromAllGroups(string hub, string user);

        // DELETE .../chat/connections/a/groups
        /// <summary>
        /// Remove a connection from all groups.
        /// </summary>
        /// <param name="hub">Target hub name, which should start with alphabetic characters and only contain alpha-numeric characters or underscore.</param>
        /// <param name="connection">Target connection Id</param>
        /// <returns></returns>
        [HttpDelete("hubs/{hub}/connections/{connection}/groups")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public abstract Task<IActionResult> RemoveConnectionFromAllGroups(string hub, string connection);

        #endregion
    }
}
