// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

public class StreamingService : IHostedService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<StreamingService> _logger;

    public StreamingService(IHubContext<ChatHub> hubContext, ILogger<StreamingService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(() => StreamingTask(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task StreamingTask(CancellationToken cancellationToken)
    {
        long counter = 0;

        _logger.LogInformation("Waiting");

        await Task.Delay(5000, cancellationToken);

        _logger.LogInformation("Spamming");

        while (!cancellationToken.IsCancellationRequested)
        {
            counter++;

            await _hubContext.Clients.All.SendAsync("ReceiveMessage", counter, counter, cancellationToken);

            await Task.Delay(1, cancellationToken);
        }
    }
}
