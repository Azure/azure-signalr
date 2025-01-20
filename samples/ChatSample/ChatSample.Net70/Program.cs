using ChatSample.Net70;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddSignalR(o => o.MaximumParallelInvocationsPerClient = 2)
    .AddAzureSignalR(o =>
    {
        o.InitialHubServerConnectionCount = 2;
        o.ConnectionString = "Endpoint=http://localhost:8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapHub<ClientResultHub>("/chat");
app.MapRazorPages();

app.MapGet("/get/{id}", async (string id, IHubContext<ClientResultHub> hubContext) =>
{
    return await hubContext.Clients.Client(id).InvokeAsync<string>("GetMessage", default);
});

app.Run();
