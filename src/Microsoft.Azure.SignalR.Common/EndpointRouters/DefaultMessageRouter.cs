// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultMessageRouter : IMessageRouter
    {
        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints)
        {
            // broadcast to all the endpoints
            return endpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
        {
            return endpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
        {
            return endpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints, note that this one is only called when the SDK is not able to identify where the connectionId is.
        /// When the outcoming connectionId happens to be also connected to this app server, SDK can directly send the messages back to that connectionId
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
        {
            // broadcast to all the endpoints and service side to do the filter
            return endpoints;
        }

        /// <summary>
        /// Only primary endpoints will be returned by client /negotiate
        /// If no primary endpoint is available, promote one secondary endpoint
        /// </summary>
        /// <returns>The availbale endpoints</returns>
        private ServiceEndpoint[] GetNegotiateEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
            var primary = endpoints.Where(s => s.Online && s.EndpointType == EndpointType.Primary).ToArray();
            if (primary.Length > 0)
            {
                return primary;
            }

            // All primary endpoints are offline, fallback to the first online secondary endpoint
            var secondary = endpoints.Where(s => s.Online && s.EndpointType == EndpointType.Secondary).ToArray();
            if (secondary.Length == 0)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return secondary;
        }
    }
}
