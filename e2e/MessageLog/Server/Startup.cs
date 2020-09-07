// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.E2ETest
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var data = new Data();

            services.AddSignalR()
                    .AddMessagePackProtocol()
                    .AddAzureSignalR(o =>
                    {
                        o.ClaimsProvider = context =>
                        {
                            data.Prefix = context.Request.Query["prefix"];
                            return new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, context.Request.Query["user"])
                            };
                        };
                        o.DiagnosticClientFilter = context =>
                        {
                            return context.Request.Query["diag"] == "true";
                        };
                    });
            services.AddSingleton(s => data);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<E2ETestHub>("/E2ETestHub");
            });
        }
    }
}
