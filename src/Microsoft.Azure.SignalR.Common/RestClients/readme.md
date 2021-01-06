# Azure SignalR Service REST API

> see https://aka.ms/autorest

This is the AutoRest configuration file for SignalR.

``` yaml
input-file: health.json
```

## Alternate settings

``` yaml $(csharp)
license-header: MICROSOFT_MIT_NO_VERSION
override-client-name: SignalRServiceRestClient
namespace: Microsoft.Azure.SignalR
output-folder: Generated
add-credentials: true
use-internal-constructors: true