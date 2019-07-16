// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public interface ITestServer
    {
        Task<string> StartAsync(Dictionary<string, string> configuration = null);
        Task StopAsync();
    }
}