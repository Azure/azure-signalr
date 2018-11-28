// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Owin;

[assembly: OwinStartup(typeof(AspNet.ChatSample.SelfHostServer.Startup))]

namespace AspNet.ChatSample.SelfHostServer
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // app.MapSignalR();
            app.UseCors(CorsOptions.AllowAll);
            app.MapAzureSignalR(GetType().FullName);
            GlobalHost.TraceManager.Switch.Level = SourceLevels.Information;
        }
    }
}
