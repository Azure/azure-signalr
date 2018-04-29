# Microsoft Azure SignalR SDK for .NET

Microsoft Azure SignalR SDK for .NET helps you to instantly build Azure applications with real-time messaging functionality, taking advantage of scalable cloud computing resources.

This repository contains the open source subset of the .NET SDK.

## Nuget Packages

Package Name | Target Framework | Version
---|---|---
Microsoft.Azure.SignalR | .NET Standard 2.0 | ![MyGet](https://img.shields.io/myget/azure-signalr-dev/v/Microsoft.Azure.SignalR.svg)

## Getting Started

1. Add below MyGet feed URL as a package source in your `NuGet.config`.

    `https://www.myget.org/F/azure-signalr-dev/api/v3/index.json`

2. Add Azure SignalR package to your project.

    ```bash
    dotnet add package Microsoft.Azure.SignalR
    ```

## Building from source

Run `build.cmd` or `build.sh` without arguments for a complete build including tests.
See [Building documents](https://github.com/aspnet/Home/wiki/Building-from-source) for more details.

## API Reference

Detailed API Reference is at [here](./docs/api-reference.md).

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
