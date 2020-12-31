# Azure SignalR Service REST API

> see https://aka.ms/autorest

This is the AutoRest configuration file for SignalR.

``` yaml
input-file: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/f0c15797105eb10d07c3dbff4680f9df95cc6201/specification/signalr/data-plane/AzureSignalR/preview/2020-10-01/azuresignalr.json
```

## Alternate settings

``` yaml $(csharp)
license-header: MICROSOFT_MIT_NO_VERSION
override-client-name: SignalRServiceRestClient
namespace: Microsoft.Azure.SignalR
output-folder: Generated
add-credentials: true
use-internal-constructors: true
sync-methods: None