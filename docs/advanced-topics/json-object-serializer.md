# Customizing Json Serialization in Management SDK
In Management SDK, the method arguments sent to clients are serialized into JSON. We have several ways to customize JSON serialization. We will show all the ways in the order from the most recommended to the least recommended.

## `ServiceManagerOptions.UseJsonObjectSerializer(ObjectSerializer objectSerializer)`
The most recommended way is to use a general abstract class [`ObjectSerializer`](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Azure.Core/1.21.0/api/Azure.Core.Serialization/Azure.Core.Serialization.ObjectSerializer.html), because it supports different JSON serialization libraries such as `System.Text.Json` and `Newtonsoft.Json` and it applies to all the transport types. Usually you don't need to implement `ObjectSerializer` yourself, as handy JSON implementations for `System.Text.Json` and `Newtonsoft.Json` are already provided.

### If you want to use `System.Text.Json` as JSON processing library
The builtin [`JsonObjectSerializer`](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Azure.Core/1.21.0/api/Azure.Core.Serialization/Azure.Core.Serialization.JsonObjectSerializer.html) uses `System.Text.Json.JsonSerializer` to for serialization/deserialization. Here is a sample to use camel case naming for JSON serialization:

```cs
var serviceManager = new ServiceManagerBuilder()
    .WithOptions(o =>
    {
        o.ConnectionString = "***";
        o.UseJsonObjectSerializer(new JsonObjectSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    })
    .BuildServiceManager();

```

### If you want to use `Newtonsoft.Json` as JSON processing library
First install the package `Microsoft.Azure.Core.NewtonsoftJson` from Nuget using .NET CLI:
```dotnetcli
dotnet add package Microsoft.Azure.Core.NewtonsoftJson
```
Here is a sample to use camel case naming with [`NewtonsoftJsonObjectSerializer`](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Microsoft.Azure.Core.NewtonsoftJson/1.0.0/index.html):

```cs
var serviceManager = new ServiceManagerBuilder()
    .WithOptions(o =>
    {
        o.ConnectionString = "***";
        o.UseJsonObjectSerializer(new NewtonsoftJsonObjectSerializer(new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        }));
    })
    .BuildServiceManager();
```

### If the above choices don't meet your requirements
You can also implement `ObjectSerializer` on your own. The following links might help:

* [API reference of `ObjectSerializer`](https://azuresdkdocs.blob.core.windows.net/$web/dotnet/Azure.Core/1.21.0/api/Azure.Core.Serialization/Azure.Core.Serialization.ObjectSerializer.html)

* [Source code of `ObjectSerializer`](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/src/Serialization/ObjectSerializer.cs)

* [Source code of `JsonObjectSerializer`](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/src/Serialization/JsonObjectSerializer.cs)


## `ServiceManagerBuilder.WithNewtonsoftJson(Action<NewtonsoftServiceHubProtocolOptions> configure)`
This method is only for `Newtonsoft.Json` users. Here is a sample to use camel case naming:
```cs
var serviceManager = new ServiceManagerBuilder()
    .WithNewtonsoftJson(o =>
    {
        o.PayloadSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    })
    .BuildServiceManager();
```

## ~~`ServiceManagerOptions.JsonSerializerSettings`~~ (Deprecated)
This method only applies to transient transport type. Don't use this.
```cs
var serviceManager = new ServiceManagerBuilder()
    .WithOptions(o =>
    {
        o.JsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
    })
    .BuildServiceManager();
```