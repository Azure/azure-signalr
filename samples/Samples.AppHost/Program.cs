// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Configure Azure SignalR resource with Standard S1 SKU
// Note: The AssignProperty API for Azure resource customization is currently in preview
// and may change in future Aspire updates. This is used here to demonstrate SKU configuration.
#pragma warning disable AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var signalr = builder.AddAzureSignalR("signalr1", (_, _, k) => k.AssignProperty(i => i.Sku.Name, "'Standard_S1'"));
#pragma warning restore AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var chatServer = builder.AddProject<ChatSample>("chat").WithReference(signalr).WithExternalHttpEndpoints().WithHttpsEndpoint();

builder.AddProject<ChatSample_CSharpClient>("csharp-client-for-chat", "auto")
    .WithEnvironment("ServerEndpoint", chatServer.GetEndpoint("https"));

builder.Build().Run();
