// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.AspNet.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Owin;

[assembly: OwinStartup(typeof(Microsoft.Azure.SignalR.E2ETest.Startup))]

namespace Microsoft.Azure.SignalR.E2ETest
{

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<Startup>(optional: true)
                .Build();

            var data = new Data();
            var resolver = new DefaultDependencyResolver();
            var hubConfiguration = new HubConfiguration
            {
                // Resolver is shared in GloblHost, use a new one instead
                Resolver = resolver
            };

            var userIdProvider = new UserIdProvider();
            hubConfiguration.Resolver.Register(typeof(IUserIdProvider), () => userIdProvider);
            
            hubConfiguration.Resolver.Register(typeof(E2ETestHub), () => new E2ETestHub(data));

            // app.MapSignalR();
            app.UseCors(CorsOptions.AllowAll);
            app.MapAzureSignalR("/signalr", GetType().FullName, hubConfiguration, options => 
            {
                options.ConnectionString = configuration["Azure:SignalR:ConnectionString"];
                options.DiagnosticClientFilter = context =>
                {
                    return context.Request.Query["diag"] == "true";
                };
                options.ClaimsProvider = context =>
                {
                    data.Prefix = context.Request.Query["prefix"];
                    return null;
                };
            });
        }

        private sealed class UserIdProvider : IUserIdProvider
        {
            public string GetUserId(IRequest request)
            {
                return request.QueryString["user"];
            }
        }
    }
}
