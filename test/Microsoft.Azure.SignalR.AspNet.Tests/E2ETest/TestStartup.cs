// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using Microsoft.AspNet.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using Owin;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestStartup
    {
        public void Configuration(IAppBuilder app)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            app.MapAzureSignalR(GetType().FullName, TestConfiguration.Instance.ConnectionString);
            GlobalHost.TraceManager.Switch.Level = SourceLevels.Information;
        }
    }
}