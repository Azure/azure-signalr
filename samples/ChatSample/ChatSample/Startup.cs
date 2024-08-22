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
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSignalR()
                .AddAzureSignalR(option =>
                {
                    option.GracefulShutdown.Mode = GracefulShutdownMode.WaitForClientsClose;
                    option.GracefulShutdown.Timeout = TimeSpan.FromSeconds(30);

                    option.GracefulShutdown.Add<Chat>(async (c) =>
                    {
                        await c.Clients.All.SendAsync("exit");
                    });
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
