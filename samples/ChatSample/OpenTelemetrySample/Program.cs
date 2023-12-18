using System.Diagnostics;
using Azure.Core;
using Azure.Monitor.OpenTelemetry.Exporter;
using ChatSample.Net60.Hubs;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Replace <azure-monitor-connectString> with your Azure Monitor connection string.
var azureMonitorString = "<azure-monitor-connect-string>";

// Add OpenTelemetry logging.
builder.Logging.AddOpenTelemetry(option =>
{
    option.AddAzureMonitorLogExporter(o =>
    {
        o.ConnectionString = azureMonitorString;
    });
});

// Add OpenTelemetry tracing.
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OpenTelemetrySample"))
    .AddSource("Azure.SignalR")
    .AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = azureMonitorString;
    })
    .AddConsoleExporter()
    .Build();

// Add services to the container.
builder.Services.AddSignalR().AddAzureSignalR();
builder.Services.AddRazorPages();

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

app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub");
app.Run();