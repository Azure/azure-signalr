# Azure SignalR Service SDK for .NET

Azure SignalR Service SDK for .NET helps you to instantly build Azure applications with real-time messaging functionality, taking advantage of scalable cloud computing resources.

This repository contains the open source subset of the .NET SDK.

## Build Status

Travis: [![travis](https://travis-ci.org/Azure/azure-signalr.svg?branch=dev)](https://travis-ci.org/Azure/azure-signalr)

## Nuget Packages

Package Name | Target Framework | NuGet | MyGet
---|---|---|---
Microsoft.Azure.SignalR | .NET Standard 2.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/v/Microsoft.Azure.SignalR.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR)
Microsoft.Azure.SignalR.Protocols | .NET Standard 2.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Protocols.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Protocols) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/v/Microsoft.Azure.SignalR.Protocols.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Protocols)

## Getting Started

Azure SignalR Service is based on [ASP.NET Core SignalR](https://github.com/aspnet/SignalR) framework. And SignalR Service SDK also depends on ASP.NET Core SignalR packages. If you are not familiar with ASP.NET Core SignalR yet, we recommend you to read [ASP.NET Core SignalR's documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/) first.

Follow the tutorial at [here](https://aka.ms/signalr_service_doc) to get started with Azure SignalR Service.

You can find more samples on how to use Azure SignalR Service at [here](https://github.com/aspnet/AzureSignalR-samples/).

## REST API support

Azure SignalR Service provides a set of REST APIs, so that you can send messages to the connected clients from anywhere using any programming language or any REST client such as [Postman](https://www.getpostman.com/). REST APIs' definition is described in [this swagger file](docs/swagger.json).

### Port

REST APIs are only exposed on port `5002`.

### Authentication

In each HTTP request, an authorization header with a [JSON Web Token (JWT)](https://en.wikipedia.org/wiki/JSON_Web_Token) is required to authenticate with Azure SignalR Service.

#### Signing Algorithm and Signature

`HS256`, namely HMAC-SHA256, is used as the signing algorithm.

You should use the `AccessKey` in Azure SignalR Service instance's connection string to sign the generated JWT token.

#### Claims

`aud` (audience) and `exp`(expiration time) are required claims in the JWT token.
- The `aud` claim should be exactly the same as your HTTP request url, trailing slash and query paramters not included. For example, a broadcast request's audience should look like as below:

    ```
    https://example.service.signalr.net:5002/api/v1-preview/hub/myhub
    ```

## Developer Getting Started

### Building from source

Run `build.cmd` or `build.sh` without arguments for a complete build including tests.
See [Building documents](https://github.com/aspnet/Home/wiki/Building-from-source) for more details.


### Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
