// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChatSample
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddAzureSignalR();

            var connStr = Configuration["AzureSignalR:ConnectionString"];
            services.AddSingleton(typeof(TokenProvider),
                CloudSignalR.CreateTokenProviderFromConnectionString(connStr));
            services.AddSingleton(typeof(EndpointProvider),
                CloudSignalR.CreateEndpointProviderFromConnectionString(connStr));

            var timeService =
                new TimeService(CloudSignalR.CreateHubProxyFromConnectionString<Chat>(connStr));
            services.AddSingleton(typeof(TimeService), timeService);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMvc();
            app.UseFileServer();
            var hubServerOptions = new HubHostOptions()
            {
                ConnectionNumber = Configuration.GetValue<int>("AzureSignalR:ServiceConnectionNo"),
                AutoReconnect = false,
                ServerTimeout = TimeSpan.FromSeconds(Configuration.GetValue<int>("AzureSignalR:ServiceTimeout"))
            };
            app.UseAzureSignalR(Configuration["AzureSignalR:ConnectionString"],
                builder => { builder.UseHub<Chat>(hubServerOptions); });
        }
    }
}
