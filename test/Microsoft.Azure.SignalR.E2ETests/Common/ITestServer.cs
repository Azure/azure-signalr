// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.E2ETests.Common;

public interface ITestServer : IDisposable
{
    Task<string> StartAsync(Dictionary<string, string?>? configuration = null);

    Task StopAsync();
}