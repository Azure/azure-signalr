// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface ICallerClientResultsManager : IClientResultsManager
    {
        string GenerateInvocationId(string connectionId);

        /// <summary>
        /// Add a invocation which is directly called by current server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connectionId"></param>
        /// <param name="invocationId"></param>
        /// <param name="instanceId"> The InstanceId of target client the caller server knows when this method is called. If the target client is managed by the caller server, the caller server knows the InstanceId of target client and this parameter is not null. Otherwise, this parameter is null. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<T> AddInvocation<T>(string connectionId, string invocationId, string instanceId, CancellationToken cancellationToken);

        void AddServiceMapping(ServiceMappingMessage serviceMappingMessage);

        void CleanupInvocationsByInstance(string instanceId);

        bool TryCompleteResult(string connectionId, ClientCompletionMessage message);

        bool TryCompleteResult(string connectionId, ErrorCompletionMessage message);
    }
}