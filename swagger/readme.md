# Azure SignalR Service REST API

> see https://aka.ms/autorest

This is the AutoRest configuration file for SignalR.

``` yaml
input-file: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/145513669e3d329e1549d714877ba720d8f0d253/specification/signalr/data-plane/AzureSignalR/v1/azuresignalr.json
```

## Alternate settings

``` yaml $(csharp)
license-header: MICROSOFT_MIT_NO_VERSION
override-client-name: GeneratedRestClient
namespace: Microsoft.Azure.SignalR
output-folder: ../src/Common/REST/
add-credentials: true