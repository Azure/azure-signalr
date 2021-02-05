// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests
{
    internal class TestHubBroadcastNCallsInvocationBinder : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName)
        {
            return new List<Type>(new Type[] { typeof(int) });
        }

        public Type GetReturnType(string invocationId)
        {
            return typeof(bool);
        }

        public Type GetStreamItemType(string streamId)
        {
            return typeof(object);
        }
    }
}
