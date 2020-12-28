# Azure SignalR Service REST API

> see https://aka.ms/autorest

This is the AutoRest configuration file for SignalR.

``` yaml
input-file: https://raw.githubusercontent.com/Azure/azure-rest-api-specs/65532763b0ae845d1e0f449af3cab7983f18e082/specification/signalr/data-plane/AzureSignalR/v1/azuresignalr.json
```

## Alternate settings

``` yaml $(csharp)
license-header: MICROSOFT_MIT_NO_VERSION
override-client-name: SignalRServiceRestClient
namespace: Microsoft.Azure.SignalR
output-folder: Generated
add-credentials: true
use-internal-constructors: true