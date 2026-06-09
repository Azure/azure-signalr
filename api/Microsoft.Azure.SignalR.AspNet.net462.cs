namespace Microsoft.Azure.SignalR.AspNet
{
    public partial class EndpointRouterDecorator : Microsoft.Azure.SignalR.AspNet.IEndpointRouter, Microsoft.Azure.SignalR.IMessageRouter
    {
        public EndpointRouterDecorator(Microsoft.Azure.SignalR.AspNet.IEndpointRouter router = null) { }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForBroadcast(System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForConnection(string connectionId, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForGroup(string groupName, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForUser(string userId, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
        public virtual Microsoft.Azure.SignalR.ServiceEndpoint GetNegotiateEndpoint(Microsoft.Owin.IOwinContext owinContext, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
    }
    public partial interface IEndpointRouter : Microsoft.Azure.SignalR.IMessageRouter
    {
        Microsoft.Azure.SignalR.ServiceEndpoint GetNegotiateEndpoint(Microsoft.Owin.IOwinContext owinContext, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints);
    }
    public partial class ServiceOptions
    {
        public ServiceOptions() { }
        public Microsoft.Azure.SignalR.AccessTokenAlgorithm AccessTokenAlgorithm { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan AccessTokenLifetime { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Func<Microsoft.Owin.IOwinContext, System.Collections.Generic.IEnumerable<System.Security.Claims.Claim>>? ClaimsProvider { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Action<System.Net.WebSockets.ClientWebSocketOptions>? ConfigureServiceConnectionWebSocketOptions { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ObsoleteAttribute("Please use InitialHubServerConnectionCount instead.")]
        public int ConnectionCount { get { throw null; } set { } }
        public string? ConnectionString { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Func<Microsoft.Owin.IOwinContext, bool>? DiagnosticClientFilter { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.ServiceEndpoint[] Endpoints { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int InitialHubServerConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? MaxHubServerConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? MaxPollIntervalInSeconds { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Net.IWebProxy? Proxy { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.ServerStickyMode ServerStickyMode { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
}
namespace Owin
{
    public static partial class OwinExtensions
    {
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string applicationName) { throw null; }
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string applicationName, Microsoft.AspNet.SignalR.HubConfiguration configuration) { throw null; }
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string applicationName, Microsoft.AspNet.SignalR.HubConfiguration configuration, System.Action<Microsoft.Azure.SignalR.AspNet.ServiceOptions> optionsConfigure) { throw null; }
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string applicationName, System.Action<Microsoft.Azure.SignalR.AspNet.ServiceOptions> optionsConfigure) { throw null; }
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string applicationName, string connectionString) { throw null; }
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string path, string applicationName, Microsoft.AspNet.SignalR.HubConfiguration configuration) { throw null; }
        public static Owin.IAppBuilder MapAzureSignalR(this Owin.IAppBuilder builder, string path, string applicationName, Microsoft.AspNet.SignalR.HubConfiguration configuration, System.Action<Microsoft.Azure.SignalR.AspNet.ServiceOptions> optionsConfigure) { throw null; }
        public static void RunAzureSignalR(this Owin.IAppBuilder builder, string applicationName) { }
        public static void RunAzureSignalR(this Owin.IAppBuilder builder, string applicationName, Microsoft.AspNet.SignalR.HubConfiguration configuration) { }
        public static void RunAzureSignalR(this Owin.IAppBuilder builder, string applicationName, Microsoft.AspNet.SignalR.HubConfiguration configuration, System.Action<Microsoft.Azure.SignalR.AspNet.ServiceOptions> optionsConfigure) { }
        public static void RunAzureSignalR(this Owin.IAppBuilder builder, string applicationName, System.Action<Microsoft.Azure.SignalR.AspNet.ServiceOptions> optionsConfigure) { }
        public static void RunAzureSignalR(this Owin.IAppBuilder builder, string applicationName, string connectionString) { }
        public static void RunAzureSignalR(this Owin.IAppBuilder builder, string applicationName, string connectionString, Microsoft.AspNet.SignalR.HubConfiguration configuration) { }
    }
}
