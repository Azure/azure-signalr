using Microsoft.AspNetCore.SignalR;

namespace ClientResultSample
{
    public class ClientResultHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            return Clients.All.SendAsync("Connect", $"Connection '{Context.ConnectionId}' is connected.");
        }

        public async Task<string> GetMessage(string ID)
        {
            try
            {
                var res = await Clients.Client(ID).InvokeAsync<string>("GetMessage", default);
                return $"From {ID}: {res}";
            }
            catch (Exception ex)
            {
                return $"[Error] Failed invoke connection {ID}]: {ex.Message}";
            }
        }

        public async Task Broadcast(string message)
        {
            await Clients.All.SendAsync("Broadcast", $"Broadcast from '{Context.ConnectionId}': {message}");
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return Clients.All.SendAsync("Connect", $"Connection '{Context.ConnectionId}' is disconnected.");
        }
    }
}
