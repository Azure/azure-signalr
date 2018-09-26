# Azure SignalR Service SDK for .NET

Azure SignalR Service SDK for .NET helps you to instantly build Azure applications with real-time messaging functionality, taking advantage of scalable cloud computing resources.

This repository contains the open source subset of the .NET SDK.

## Build Status

[![Travis build status](https://img.shields.io/travis/Azure/azure-signalr.svg?label=travis-ci&branch=dev&style=flat-square)](https://travis-ci.org/Azure/azure-signalr/branches)
[![AppVeyor build status](https://img.shields.io/appveyor/ci/vicancy/azure-signalr/dev.svg?label=appveyor&style=flat-square)](https://ci.appveyor.com/project/vicancy/azure-signalr)

## Nuget Packages

Package Name | Target Framework | NuGet | MyGet
---|---|---|---
Microsoft.Azure.SignalR | .NET Standard 2.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/v/Microsoft.Azure.SignalR.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR)
Microsoft.Azure.SignalR.Protocols | .NET Standard 2.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Protocols.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Protocols) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/v/Microsoft.Azure.SignalR.Protocols.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Protocols)
Microsoft.Azure.SignalR.AspNet | .NET Standard 2.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.AspNet.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.AspNet) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/v/Microsoft.Azure.SignalR.AspNet.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.AspNet)

## Getting Started

Azure SignalR Service supports both [ASP.NET Core SignalR](https://github.com/aspnet/SignalR) and [ASP.NET SignalR](https://github.com/SignalR/SignalR). The support for [ASP.NET Core SignalR](https://github.com/aspnet/SignalR) is generally availble, while the support for [ASP.NET SignalR](https://github.com/SignalR/SignalR) is in preview stage at this time.

### ASP.NET Core SignalR
Package [Microsoft.Azure.SignalR](https://www.nuget.org/packages/Microsoft.Azure.SignalR) is the one to use when you are using [ASP.NET Core SignalR](https://github.com/aspnet/SignalR). If you are not familiar with ASP.NET Core SignalR yet, we recommend you to read [ASP.NET Core SignalR's documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/) first.

Follow the tutorial at [here](https://aka.ms/signalr_service_doc) to get started with Azure SignalR Service.

More samples on how to use Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/).

> Note
> 
> There're two versions of SignalR: [ASP.NET SignalR](https://github.com/SignalR/SignalR) and [ASP.NET Core SignalR](https://github.com/aspnet/SignalR). The ASP.NET Core version is not a simple .NET Core port of the original SignalR, but a [rewrite](https://blogs.msdn.microsoft.com/webdev/2017/09/14/announcing-signalr-for-asp-net-core-2-0/) of the original version. As a result, [ASP.NET Core SignalR](https://github.com/aspnet/SignalR) is not backward compatible with [ASP.NET SignalR](https://github.com/SignalR/SignalR) (API interfaces and behaviors are different). If it is the first time you try SignalR, we recommend you to use the [ASP.NET Core SignalR](https://github.com/aspnet/SignalR), it is **simpler, more reliable, and easier to use**.

### ASP.NET SignalR
Package [Microsoft.Azure.SignalR.AspNet](https://www.nuget.org/packages/Microsoft.Azure.SignalR.AspNet) is the one to use when you are using [ASP.NET SignalR](https://github.com/SignalR/SignalR). If you are not familiar with ASP.NET SignalR yet, we recommend you to read [ASP.NET SignalR's documentation](https://docs.microsoft.com/en-us/aspnet/signalr/) first.

Samples on how to use Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/tree/master/aspnet-samples/ChatRoom)

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
