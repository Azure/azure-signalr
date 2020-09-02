# Azure SignalR Service REST API

> see https://aka.ms/autorest

This is the AutoRest configuration file for SignalR.

``` yaml
input-file: ../src/Microsoft.Azure.SignalR.Common/RestClients/health.json
```

## Alternate settings

``` yaml $(csharp)
license-header: MICROSOFT_MIT_NO_VERSION
override-client-name: SignalRServiceRestClient
namespace: Microsoft.Azure.SignalR
output-folder: ../src/Microsoft.Azure.SignalR.Common/RestClients/Generated
add-credentials: true