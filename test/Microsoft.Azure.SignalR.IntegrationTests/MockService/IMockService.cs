// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    // Trimmed for skeleton integration test
    internal interface IMockService 
    {
        public IDuplexPipe MockServicePipe { get; set; }
        public Task StartAsync();
        public Task StopAsync();

        // ...
        // the rest is omitted for skeleton test PR
        public TaskCompletionSource<bool> CompletedServiceConnectionHandshake { get; }
    }
}