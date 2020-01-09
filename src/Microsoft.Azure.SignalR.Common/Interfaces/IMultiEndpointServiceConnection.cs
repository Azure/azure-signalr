using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal interface IMultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        /// <summary>
        /// Create IServiceConnectionContainer for new ServiceEndpoint and start server connections
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="loggerFactory"></param>
        /// <returns>add result</returns>
        bool AddServiceEndpoint(HubServiceEndpoint endpoint, ILoggerFactory loggerFactory);

        /// <summary>
        /// Label to detect whether New ServiceEndpoint is ready to open route
        /// </summary>
        bool IsStable { get; }

        /// <summary>
        /// Label to detect whether ServiceEndpoint has clients and ready to remove
        /// </summary>
        bool IsActive { get; }
    }
}