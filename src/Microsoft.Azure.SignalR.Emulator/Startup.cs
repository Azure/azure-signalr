// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Azure.SignalR.Emulator.Controllers;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Emulator
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();

            services.AddAllowAllCors();
            services.AddJwtBearerAuth(Configuration);
            services.AddAuthorization();

            services.AddControllers().AddNewtonsoftJson().ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Add(new CustomControllerFeatureProvider());
            });

            services.Configure<UpstreamOptions>(Configuration.GetSection("UpstreamSettings"));

            services.AddSignalREmulator();
            services.AddLogging(services =>
            {
                services.AddDebug();
                services.AddConsole();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            lifetime.ApplicationStarted.Register(() =>
               {
                   var address = new Uri(app.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First());
                   var upstreamOptionMonitor = app.ApplicationServices.GetRequiredService<IOptionsMonitor<UpstreamOptions>>();
                   upstreamOptionMonitor.OnChange(s =>
                   {
                       s.Print();
                   });
                   Console.WriteLine(@$"
===================================================
The Azure SignalR Emulator was successfully started.

Press Ctrl+C to stop the Emulator.

Use the below value inside *********** block as its ConnectionString:
***********

Endpoint={address.Scheme}://{address.Host};Port={address.Port};AccessKey={AppBuilderExtensions.AccessKey};Version=1.0;

***********

===================================================
");
                   upstreamOptionMonitor.CurrentValue.Print();


               });
            app.UseRouting();
            app.UseWebSockets();
            app.UseAllowAllCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints
                    .MapConnections("/client", cb => cb.UseConnectionHandler<DynamicConnectionHandler>())
                    .RequireAuthorization();

                endpoints.MapControllers();
            });
        }

        private sealed class CustomControllerFeatureProvider : ControllerFeatureProvider
        {
            protected override bool IsController(TypeInfo typeInfo)
            {
                var isCustomController = !typeInfo.IsAbstract && typeof(SignalRServiceEmulatorWebApi).IsAssignableFrom(typeInfo);
                return isCustomController || base.IsController(typeInfo);
            }
        }
    }
}
