using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton<IUserIdProvider, MyUserIdProvider>();
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
                        new ServiceEndpoint("Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;"),
                        new ServiceEndpoint("Endpoint=http://localhost;Port=8081;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;", type: EndpointType.Primary, name: "backup")
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
