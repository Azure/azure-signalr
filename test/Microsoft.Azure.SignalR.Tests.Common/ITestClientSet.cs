// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public interface ITestClientSet
    {
        Task StartAsync();
        Task StopAsync();
        int Count { get; }
        void AddListener(string methodName, Action<string> handler);
        Task AllSendAsync(string methodName, string message);
    }
}