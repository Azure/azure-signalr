using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Management;

namespace ManagementSample
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        static async Task MainAsync() {
            var manager = new ServiceManagerBuilder().WithOptions(o =>
            {
            }).Build();

            var hub = await manager.CreateHubContextAsync("foo");

            await hub.Clients.All.SendAsync("asd");
        }
    }
}
