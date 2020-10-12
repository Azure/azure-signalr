using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChatSample.CoreApp3
{
    public class Startup
    {
        internal class MyUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection)
            {
                return connection.GetHttpContext().Request.Query["user"];
            }
        }
        private class CustomRouter : EndpointRouterDecorator
        {
            public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
            {
                // Override the negotiate behavior to get the endpoint from query string
                var endpointName = context.Request.Query["endpoint"];
                if (endpointName.Count == 0)
                {
                    context.Response.StatusCode = 400;
                    var response = Encoding.UTF8.GetBytes("Invalid request");
                    context.Response.Body.Write(response, 0, response.Length);
                    return null;
                }

                return endpoints.FirstOrDefault(s => s.Name == endpointName && s.Online) // Get the endpoint with name matching the incoming request
                       ?? base.GetNegotiateEndpoint(context, endpoints); // Or fallback to the default behavior to randomly select one from primary endpoints, or fallback to secondary when no primary ones are online
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
            {
                return base.GetEndpointsForUser(userId, endpoints).Where(s => s.Online).Take(1);
            }

        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton<IUserIdProvider, MyUserIdProvider>();
            services.AddSingleton(typeof(IEndpointRouter), typeof(CustomRouter));
            services.AddSignalR()
                .AddAzureSignalR(option =>
                {
                    option.GracefulShutdown.Mode = GracefulShutdownMode.WaitForClientsClose;
                    option.GracefulShutdown.Timeout = TimeSpan.FromSeconds(10);
                    option.Endpoints = new ServiceEndpoint[]
                    {
                        // Note: this is just a demonstration of how to set options.Endpoints
                        // Having ConnectionStrings explicitly set inside the code is not encouraged
                        // You can fetch it from a safe place such as Azure KeyVault
                        new ServiceEndpoint(name:"primary", connectionString: "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;"),
                        new ServiceEndpoint(name:"backup", connectionString: "Endpoint=http://localhost;Port=8081;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;")
                    };
                })
                .AddMessagePackProtocol();
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseFileServer();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(routes =>
            {
                routes.MapHub<Chat>("/chat");
                routes.MapHub<BenchHub>("/signalrbench");
            });
        }
    }
}
