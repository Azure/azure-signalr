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

                    option.ConnectionString = "Endpoint=http://localhost;Port=8080;AuthType=aad;ClientId=604aca3a-0b10-4a04-8a7f-dfb59137a607;ClientSecret=yRM~82qhk87naAhubq05f-f3_p.WN_H~gN;TenantId=72f988bf-86f1-41af-91ab-2d7cd011db47;Version=1.0;";

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
