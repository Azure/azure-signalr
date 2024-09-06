// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Projects;

var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var signalr = builder.AddAzureSignalR("signalr1", (_, _, k) => k.AssignProperty(i => i.Sku.Name, "'Standard_S1'"));
#pragma warning restore AZPROVISION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var chatServer = builder.AddProject<ChatSample>("chat").WithReference(signalr).WithExternalHttpEndpoints().WithHttpsEndpoint();

builder.AddProject<ChatSample_CSharpClient>("csharp-client-for-chat", "auto")
    .WithEnvironment("ServerEndpoint", chatServer.GetEndpoint("https"));

builder.AddProject<ChatSample_Net60>("chat-net6").WithReference(signalr).WithExternalHttpEndpoints();
builder.AddProject<ChatSample_Net70>("chat-net7-client-invocation").WithReference(signalr).WithExternalHttpEndpoints();

builder.Build().Run();