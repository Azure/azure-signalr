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
        /// <param name="hub"></param>
        /// <param name="connectionId"></param>
        /// <param name="invocationId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<T> AddInvocation<T>(string hub, string connectionId, string invocationId, CancellationToken cancellationToken);

        void AddServiceMapping(ServiceMappingMessage serviceMappingMessage);

        void CleanupInvocationsByInstance(string instanceId);

        bool TryCompleteResult(string connectionId, ClientCompletionMessage message);

        bool TryCompleteResult(string connectionId, ErrorCompletionMessage message);

        void RemoveInvocation(string invocationId);
    }
}