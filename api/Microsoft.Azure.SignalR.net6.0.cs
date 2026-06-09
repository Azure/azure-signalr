namespace Microsoft.AspNetCore.Builder
{
    public static partial class AzureSignalRApplicationBuilderExtensions
    {
        [System.ObsoleteAttribute("IApplicationBuilder.UseAzureSignalR is obsoleted, please use IApplicationBuilder.UseEndpoints() instead.")]
        public static Microsoft.AspNetCore.Builder.IApplicationBuilder UseAzureSignalR(this Microsoft.AspNetCore.Builder.IApplicationBuilder app, System.Action<Microsoft.Azure.SignalR.ServiceRouteBuilder> configure) { throw null; }
    }
}
namespace Microsoft.Azure.SignalR
{
    public partial class EndpointRouterDecorator : Microsoft.Azure.SignalR.IEndpointRouter, Microsoft.Azure.SignalR.IMessageRouter
    {
        public EndpointRouterDecorator(Microsoft.Azure.SignalR.IEndpointRouter router = null) { }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForBroadcast(System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForConnection(string connectionId, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForGroup(string groupName, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForUser(string userId, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual Microsoft.Azure.SignalR.ServiceEndpoint GetNegotiateEndpoint(Microsoft.AspNetCore.Http.HttpContext context, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
    }
    public partial class GracefulShutdownOptions
    {
        public GracefulShutdownOptions() { }
        public Microsoft.Azure.SignalR.GracefulShutdownMode Mode { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan Timeout { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public void Add<THub>(System.Action action) { }
        public void Add<THub>(System.Action<Microsoft.AspNetCore.SignalR.IHubContext<THub>> action) where THub : Microsoft.AspNetCore.SignalR.Hub { }
        public void Add<THub>(System.Func<Microsoft.AspNetCore.SignalR.IHubContext<THub>, System.Threading.Tasks.Task> func) where THub : Microsoft.AspNetCore.SignalR.Hub { }
        public void Add<THub>(System.Func<System.Threading.Tasks.Task> func) { }
    }
    public partial interface IConnectionMigrationFeature
    {
        string MigrateFrom { get; }
        string MigrateTo { get; }
    }
    public partial interface IConnectionStatFeature
    {
        System.DateTime LastMessageReceivedAtUtc { get; }
        long ReceivedBytes { get; }
        System.DateTime StartedAtUtc { get; }
    }
    public partial interface IEndpointRouter : Microsoft.Azure.SignalR.IMessageRouter
    {
        Microsoft.Azure.SignalR.ServiceEndpoint GetNegotiateEndpoint(Microsoft.AspNetCore.Http.HttpContext context, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints);
    }
    public partial class ServiceOptions
    {
        public ServiceOptions() { }
        public Microsoft.Azure.SignalR.AccessTokenAlgorithm AccessTokenAlgorithm { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan AccessTokenLifetime { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public bool? AllowStatefulReconnects { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? ApplicationName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Func<Microsoft.AspNetCore.Http.HttpContext, System.Collections.Generic.IEnumerable<System.Security.Claims.Claim>>? ClaimsProvider { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ObsoleteAttribute("Please use InitialHubServerConnectionCount instead.")]
        public int ConnectionCount { get { throw null; } set { } }
        public string? ConnectionString { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Func<Microsoft.AspNetCore.Http.HttpContext, bool>? DiagnosticClientFilter { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.ServiceEndpoint[]? Endpoints { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.GracefulShutdownOptions GracefulShutdown { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int InitialHubServerConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? MaxHubServerConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? MaxPollIntervalInSeconds { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Net.IWebProxy? Proxy { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.ServerStickyMode ServerStickyMode { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan ServiceScaleTimeout { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Func<Microsoft.AspNetCore.Http.HttpContext, Microsoft.AspNetCore.Http.Connections.HttpTransportType>? TransportTypeDetector { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ServiceRouteBuilder
    {
        public ServiceRouteBuilder(Microsoft.AspNetCore.Routing.RouteBuilder routes) { }
        public void MapHub<THub>(Microsoft.AspNetCore.Http.PathString path) where THub : Microsoft.AspNetCore.SignalR.Hub { }
        public void MapHub<THub>(string path) where THub : Microsoft.AspNetCore.SignalR.Hub { }
    }
}
namespace Microsoft.Extensions.DependencyInjection
{
    public static partial class AzureSignalRDependencyInjectionExtensions
    {
        public static Microsoft.AspNetCore.SignalR.ISignalRServerBuilder AddAzureSignalR(this Microsoft.AspNetCore.SignalR.ISignalRServerBuilder builder) { throw null; }
        public static Microsoft.AspNetCore.SignalR.ISignalRServerBuilder AddAzureSignalR(this Microsoft.AspNetCore.SignalR.ISignalRServerBuilder builder, System.Action<Microsoft.Azure.SignalR.ServiceOptions> configure) { throw null; }
        public static Microsoft.AspNetCore.SignalR.ISignalRServerBuilder AddAzureSignalR(this Microsoft.AspNetCore.SignalR.ISignalRServerBuilder builder, string connectionString) { throw null; }
        public static Microsoft.AspNetCore.SignalR.ISignalRServerBuilder AddNamedAzureSignalR(this Microsoft.AspNetCore.SignalR.ISignalRServerBuilder builder, string name) { throw null; }
    }
}
