﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IServiceConnectionManager : IServiceConnectionContainer
    {
        void Initialize(Func<string, IServiceConnection> connectionGenerator, int connectionCount);

        IServiceConnectionContainer WithHub(string hubName);
    }
}