# Azure SignalR Service SDK for .NET

Azure SignalR Service SDK for .NET helps you to instantly build Azure applications with real-time messaging functionality, taking advantage of scalable cloud computing resources.

This repository contains the open source subset of the .NET SDK.

## Build Status

[![Windows](https://img.shields.io/github/actions/workflow/status/Azure/azure-signalr/windows.yml?branch=dev&label=Windows)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-Windowns)
[![Ubuntu](https://img.shields.io/github/actions/workflow/status/Azure/azure-signalr/ubuntu.yml?branch=dev&label=Ubuntu)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-Ubuntu)
[![OSX](https://img.shields.io/github/actions/workflow/status/Azure/azure-signalr/osx.yml?branch=dev&label=OSX)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-OSX)

## Nuget Packages

Azure SignalR Service SDK is supporting ASP.NET Core 3.0 from version `1.1.0-*`. Please find package information below.

<div class="packageTable">
  
Package Name | Description | Target Frameworks | <img width=500/> Packages <img width=500/>
---|---|---|---
Microsoft.Azure.SignalR.AspNet | The package to use when you are using **ASP.NET SignalR** | **.NETFramework 4.6.1** | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.AspNet.svg?label=NuGet)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.AspNet) <br/> [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.AspNet.svg?label=MyGet)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.AspNet)
Microsoft.Azure.SignalR | The package to use when you are using **ASP.NET Core SignalR** | **.NET Standard 2.0**<br/> **.NET Core App 3.1**<br/> **.NET 5.0**<br/> **.NET 6.0**<br/> **.NET 7.0** |  [![Nuget](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.svg?label=NuGet)](https://www.nuget.org/packages/Microsoft.Azure.SignalR/) <br/> [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.svg?label=MyGet)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR)
Microsoft.Azure.SignalR.Management | You can use the package to manage **ASP.NET Core SignalR** clients through Azure SignalR Service directly | **.NET Standard 2.0**<br/> **.NET Core App 3.1**<br/> **.NET 5.0**<br/> **.NET 6.0**<br/> **.NET 7.0**  | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Management.svg?label=NuGet)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) <br/>  [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.Management.svg?label=MyGet)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Management)
| Microsoft.Azure.SignalR.Protocols | The package contains the data protocol between the SDK and the Azure SignalR Service | **.NET Standard 2.0** | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Protocols.svg?label=NuGet)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Protocols) <br/>  [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.Protocols.svg?label=MyGet)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Protocols)
| Microsoft.Azure.SignalR.Emulator | The emulator tool for serverless scenarios | **.NET 6.0** | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Emulator.svg?label=NuGet)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Emulator) <br/>  [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.Emulator.svg?label=MyGet)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Emulator)
</div>

## Getting Started

Azure SignalR Service is based on [ASP.NET Core SignalR](https://github.com/aspnet/AspNetCore/tree/master/src/SignalR) framework, and it supports both [ASP.NET Core SignalR](https://github.com/aspnet/AspNetCore/tree/master/src/SignalR) and [ASP.NET SignalR](https://github.com/SignalR/SignalR) applications. Both support are generally available. Please note that different frameworks require different Azure SignalR SDK, for ASP.NET Core SignalR, it is package `Microsoft.Azure.SignalR` to install, for ASP.NET SignalR, it is package `Microsoft.Azure.SignalR.AspNet`.

### ASP.NET Core SignalR

Package [Microsoft.Azure.SignalR](https://www.nuget.org/packages/Microsoft.Azure.SignalR) is the one to use when you are using [ASP.NET Core SignalR](https://github.com/aspnet/SignalR). If you are not familiar with ASP.NET Core SignalR yet, we recommend you to read [ASP.NET Core SignalR's documentation](https://docs.microsoft.com/aspnet/core/signalr/) first.

Follow the tutorial at [here](https://aka.ms/signalr_service_doc) to get started with Azure SignalR Service.

More samples on how to use Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/).

> Note
> 
> There're two versions of SignalR: [ASP.NET SignalR](https://github.com/SignalR/SignalR) and [ASP.NET Core SignalR](https://github.com/aspnet/SignalR). The ASP.NET Core version is not a simple .NET Core port of the original SignalR, but a [rewrite](https://blogs.msdn.microsoft.com/webdev/2017/09/14/announcing-signalr-for-asp-net-core-2-0/) of the original version. As a result, [ASP.NET Core SignalR](https://github.com/aspnet/SignalR) is not backward compatible with [ASP.NET SignalR](https://github.com/SignalR/SignalR) (API interfaces and behaviors are different). If it is the first time you try SignalR, we recommend you to use the [ASP.NET Core SignalR](https://github.com/aspnet/SignalR), it is **simpler, more reliable, and easier to use**.

### ASP.NET SignalR

> Note:
>
> Please make sure ASP.NET SignalR client version is using 2.4.0 or above.

Package [Microsoft.Azure.SignalR.AspNet](https://www.nuget.org/packages/Microsoft.Azure.SignalR.AspNet) is the one to use when you are using [ASP.NET SignalR](https://github.com/SignalR/SignalR). If you are not familiar with ASP.NET SignalR yet, we recommend you to read [ASP.NET SignalR's documentation](https://docs.microsoft.com/en-us/aspnet/signalr/) first.

Samples on how to use Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/tree/master/aspnet-samples/ChatRoom)

### Management

> Note: 
> 
> Management API only supports **ASP.NET Core SignalR**.

Package [Microsoft.Azure.SignalR.Management](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) is the one to use when you want to manage SignalR clients through Azure SignalR Service directly such as broadcast messages. This SDK can be but not limited to be used in [serverless](https://azure.microsoft.com/solutions/serverless/) environments. You can use this SDK to manage SignalR clients connected to your Azure SignalR Service in any environment, such as in a console app, in an Azure function or in an App Server.

More details can be found [here](https://learn.microsoft.com/azure/azure-signalr/signalr-howto-use-management-sdk).

The sample on how to use Management SDK to redirect SignalR clients to Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/tree/master/samples/Management).

## Next Steps

The following documents describe more details about Azure SignalR Service.

- [Use Azure SignalR Service](https://docs.microsoft.com/azure/azure-signalr/signalr-howto-use)
- [REST API in Azure SignalR Service](https://docs.microsoft.com/azure/azure-signalr/signalr-reference-data-plane-rest-api)
- [Internals of the Azure SignalR Service](https://docs.microsoft.com/azure/azure-signalr/signalr-concept-internals)
- [FAQ](https://docs.microsoft.com/azure/azure-signalr/signalr-resource-faq)
- [Troubleshooting Guide](https://docs.microsoft.com/azure/azure-signalr/signalr-howto-troubleshoot-guide)
- [Azure SignalR Local Emulator](https://learn.microsoft.com/azure/azure-signalr/signalr-howto-emulator)

Contributions are highly welcome. Keep reading if you want to contribute to our repository.

### Building from source

See [Building Documents](./docs/build-source.md) for more details.

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

## Performance

See [Performance](https://docs.microsoft.com/azure/azure-signalr/signalr-concept-performance) for details.
