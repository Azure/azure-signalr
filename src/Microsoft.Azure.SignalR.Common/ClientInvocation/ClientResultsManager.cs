// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ClientResultsManager : IClientResultsManager, IInvocationBinder
    {
        public ClientResultsManager(IHubProtocolResolver hubProtocolResolver)
        {
            throw new NotImplementedException();
        }

        public string GetNewInvocationId(string connectionId, string serverGUID)
        {
            throw new NotImplementedException();
        }

        public Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void AddServiceMappingMessage(string invocationId, ServiceMappingMessage serviceMappingMessage)
        {
            throw new NotImplementedException();
        }

        public void TryCompleteResult(string connectionId, CompletionMessage message)
        {
            throw new NotImplementedException();
        }

        public void TryCompleteResultFromSerializedMessage(string connectionId, string protocol, ReadOnlySequence<byte> message)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveInvocation(string invocationId, out PendingInvocation invocation)
        {
            throw new NotImplementedException();
        }

        // Implemented for interface IInvocationBinder
        public Type GetReturnType(string invocationId)
        {
            throw new NotImplementedException();
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            throw new NotImplementedException();
        }

        public void CleanupInvocations(string instanceId)
        {
            throw new NotImplementedException();
        }

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public IReadOnlyList<Type> GetParameterTypes(string methodName)
        {
            throw new NotImplementedException();
        }

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public Type GetStreamItemType(string streamId)
        {
            throw new NotImplementedException();
        }
#pragma warning restore IDE0060 // Remove unused parameter
    }
}
#else
namespace Microsoft.Azure.SignalR
{ 
    internal class ClientResultsManager: IClientResultsManager
    {

    }
}
#endif