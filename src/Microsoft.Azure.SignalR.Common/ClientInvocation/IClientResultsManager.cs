﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientResultsManager
    {
        bool TryCompleteResult(string connectionId, CompletionMessage message);

        bool TryGetInvocationReturnType(string invocationId, out Type type);

        void AddServiceMapping(ServiceMappingMessage serviceMappingMessage);

        void RemoveServiceMapping(string invocationId);

        void CleanupInvocations(string instanceId);
    }
}