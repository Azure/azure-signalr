// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ChatSample.AspNet.Startup))]

namespace ChatSample.AspNet
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Turn tracing on programmatically
            GlobalHost.TraceManager.Switch.Level = SourceLevels.Information;

            app.RunSignalR();
        }
    }
}
