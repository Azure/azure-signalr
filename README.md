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

More samples on how to use Azure SignalR Service can be found at [here](https://github.com/aspnet/AzureSignalR-samples/).

## Next Steps

For more information, see the following resources.

- [REST API support](./docs/rest-api.md)

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
