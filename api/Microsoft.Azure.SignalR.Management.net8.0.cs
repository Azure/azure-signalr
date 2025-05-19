namespace Microsoft.Azure.SignalR.Management
{
    public abstract partial class ClientManager
    {
        protected ClientManager() { }
        public abstract System.Threading.Tasks.Task CloseConnectionAsync(string connectionId, string reason = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task<bool> ConnectionExistsAsync(string connectionId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task<bool> GroupExistsAsync(string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task<bool> UserExistsAsync(string userId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
    public abstract partial class GroupManager : Microsoft.AspNetCore.SignalR.IGroupManager
    {
        protected GroupManager() { }
        public abstract System.Threading.Tasks.Task AddToGroupAsync(string connectionId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task RemoveFromAllGroupsAsync(string connectionId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task RemoveFromGroupAsync(string connectionId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
    public partial interface IServiceHubContext : Microsoft.AspNetCore.SignalR.IHubContext<Microsoft.AspNetCore.SignalR.Hub>
    {
        Microsoft.Azure.SignalR.Management.IUserGroupManager UserGroups { get; }
        System.Threading.Tasks.Task DisposeAsync();
    }
    public partial interface IServiceManager : System.IDisposable
    {
        System.Threading.Tasks.Task<Microsoft.Azure.SignalR.Management.IServiceHubContext> CreateHubContextAsync(string hubName, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        string GenerateClientAccessToken(string hubName, string userId = null, System.Collections.Generic.IList<System.Security.Claims.Claim> claims = null, System.TimeSpan? lifeTime = default(System.TimeSpan?));
        string GetClientEndpoint(string hubName);
        System.Threading.Tasks.Task<bool> IsServiceHealthy(System.Threading.CancellationToken cancellationToken);
    }
    [System.ObsoleteAttribute("Use ServiceManagerBuilder.BuildServiceManager() to build an abstract class of ServiceManager instead.")]
    public partial interface IServiceManagerBuilder
    {
        [System.ObsoleteAttribute("Use ServiceManagerBuilder.BuildServiceManager() instead.")]
        Microsoft.Azure.SignalR.Management.IServiceManager Build();
    }
    public partial interface IStreamingManager
    {
        System.Threading.Tasks.Task SendStreamAsync<TItem>(string connectionId, string streamId, System.Collections.Generic.IAsyncEnumerable<TItem> items, System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task SendStreamAsync<TItem>(string connectionId, string streamId, System.Threading.Channels.ChannelReader<TItem> items, System.Threading.CancellationToken cancellationToken);
    }
    public partial interface IUserGroupManager
    {
        System.Threading.Tasks.Task AddToGroupAsync(string userId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        System.Threading.Tasks.Task AddToGroupAsync(string userId, string groupName, System.TimeSpan ttl, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        System.Threading.Tasks.Task<bool> IsUserInGroup(string userId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        System.Threading.Tasks.Task RemoveFromAllGroupsAsync(string userId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        System.Threading.Tasks.Task RemoveFromGroupAsync(string userId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
    public partial class ManagementHubOptionsSetup : Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.SignalR.HubOptions>
    {
        public ManagementHubOptionsSetup(System.Collections.Generic.IEnumerable<Microsoft.AspNetCore.SignalR.Protocol.IHubProtocol> protocols) { }
        public void Configure(Microsoft.AspNetCore.SignalR.HubOptions options) { }
    }
    public partial class NegotiationOptions
    {
        public NegotiationOptions() { }
        public System.Collections.Generic.IList<System.Security.Claims.Claim> Claims { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public bool CloseOnAuthenticationExpiration { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public bool EnableDetailedErrors { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.AspNetCore.Http.HttpContext HttpContext { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public bool IsDiagnosticClient { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan TokenLifetime { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class NewtonsoftServiceHubProtocolOptions
    {
        public NewtonsoftServiceHubProtocolOptions() { }
        public Newtonsoft.Json.JsonSerializerSettings PayloadSerializerSettings { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class ServiceHubContext : Microsoft.AspNetCore.SignalR.IHubContext<Microsoft.AspNetCore.SignalR.Hub>, Microsoft.Azure.SignalR.Management.IServiceHubContext, System.IDisposable
    {
        protected ServiceHubContext() { }
        public virtual Microsoft.Azure.SignalR.Management.ClientManager ClientManager { get { throw null; } }
        public virtual Microsoft.AspNetCore.SignalR.IHubClients Clients { get { throw null; } }
        public virtual Microsoft.Azure.SignalR.Management.GroupManager Groups { get { throw null; } }
        Microsoft.AspNetCore.SignalR.IGroupManager Microsoft.AspNetCore.SignalR.IHubContext<Microsoft.AspNetCore.SignalR.Hub>.Groups { get { throw null; } }
        Microsoft.Azure.SignalR.Management.IUserGroupManager Microsoft.Azure.SignalR.Management.IServiceHubContext.UserGroups { get { throw null; } }
        public virtual Microsoft.Azure.SignalR.Management.StreamingManager Streaming { get { throw null; } }
        public virtual Microsoft.Azure.SignalR.Management.UserGroupManager UserGroups { get { throw null; } }
        public virtual void Dispose() { }
        public virtual System.Threading.Tasks.Task DisposeAsync() { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetServiceEndpoints() { throw null; }
        public virtual System.Threading.Tasks.ValueTask<Microsoft.AspNetCore.Http.Connections.NegotiationResponse> NegotiateAsync(Microsoft.Azure.SignalR.Management.NegotiationOptions negotiationOptions = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public virtual Microsoft.Azure.SignalR.Management.ServiceHubContext WithEndpoints(System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints) { throw null; }
    }
    public abstract partial class ServiceHubContext<T> : Microsoft.AspNetCore.SignalR.IHubContext<Microsoft.AspNetCore.SignalR.Hub<T>, T>, System.IAsyncDisposable, System.IDisposable where T : class
    {
        protected ServiceHubContext() { }
        public abstract Microsoft.Azure.SignalR.Management.ClientManager ClientManager { get; }
        public abstract Microsoft.AspNetCore.SignalR.IHubClients<T> Clients { get; }
        public abstract Microsoft.Azure.SignalR.Management.GroupManager Groups { get; }
        Microsoft.AspNetCore.SignalR.IGroupManager Microsoft.AspNetCore.SignalR.IHubContext<Microsoft.AspNetCore.SignalR.Hub<T>,T>.Groups { get { throw null; } }
        public abstract Microsoft.Azure.SignalR.Management.UserGroupManager UserGroups { get; }
        public abstract void Dispose();
        public abstract System.Threading.Tasks.ValueTask DisposeAsync();
        public abstract System.Threading.Tasks.ValueTask<Microsoft.AspNetCore.Http.Connections.NegotiationResponse> NegotiateAsync(Microsoft.Azure.SignalR.Management.NegotiationOptions negotiationOptions = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
    public abstract partial class ServiceManager : System.IDisposable
    {
        protected ServiceManager() { }
        public abstract System.Threading.Tasks.Task<Microsoft.Azure.SignalR.Management.ServiceHubContext> CreateHubContextAsync(string hubName, System.Threading.CancellationToken cancellationToken);
        public virtual System.Threading.Tasks.Task<Microsoft.Azure.SignalR.Management.ServiceHubContext<T>> CreateHubContextAsync<T>(string hubName, System.Threading.CancellationToken cancellationToken) where T : class { throw null; }
        public abstract void Dispose();
        public abstract System.Threading.Tasks.Task<bool> IsServiceHealthy(System.Threading.CancellationToken cancellationToken);
    }
    public partial class ServiceManagerBuilder : Microsoft.Azure.SignalR.Management.IServiceManagerBuilder
    {
        public ServiceManagerBuilder() { }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder AddHubProtocol(Microsoft.AspNetCore.SignalR.Protocol.IHubProtocol hubProtocol) { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder AddUserAgent(string userAgent) { throw null; }
        [System.ObsoleteAttribute("Use BuildServiceManager() instead. See https://github.com/Azure/azure-signalr/blob/dev/docs/management-sdk-migration.md for migration guide.")]
        public Microsoft.Azure.SignalR.Management.IServiceManager Build() { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManager BuildServiceManager() { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithCallingAssembly() { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration) { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithHubProtocols(params Microsoft.AspNetCore.SignalR.Protocol.IHubProtocol[] hubProtocols) { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory) { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithNewtonsoftJson() { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithNewtonsoftJson(System.Action<Microsoft.Azure.SignalR.Management.NewtonsoftServiceHubProtocolOptions> configure) { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithOptions(System.Action<Microsoft.Azure.SignalR.Management.ServiceManagerOptions> configure) { throw null; }
        public Microsoft.Azure.SignalR.Management.ServiceManagerBuilder WithRouter(Microsoft.Azure.SignalR.IEndpointRouter router) { throw null; }
    }
    [System.Runtime.CompilerServices.NullableAttribute((byte)0)]
    [System.Runtime.CompilerServices.NullableContextAttribute((byte)2)]
    public partial class ServiceManagerOptions
    {
        public ServiceManagerOptions() { }
        public string? ApplicationName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int ConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? ConnectionString { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan HttpClientTimeout { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.ObsoleteAttribute("Use ServiceManagerBuilder.WithNewtonsoftJson instead.")]
        [System.Runtime.CompilerServices.NullableAttribute((byte)1)]
        public Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings { [System.Runtime.CompilerServices.CompilerGeneratedAttribute, System.Runtime.CompilerServices.NullableContextAttribute((byte)1)] get { throw null; } }
        public System.Net.IWebProxy? Proxy { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.Management.ServiceManagerRetryOptions? RetryOptions { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.Runtime.CompilerServices.NullableAttribute(new byte[]{ (byte)2, (byte)1})]
        [System.Runtime.CompilerServices.NullableAttribute(new byte[]{ (byte)2, (byte)1})]
        [System.Runtime.CompilerServices.NullableAttribute(new byte[]{ (byte)2, (byte)1})]
        public Microsoft.Azure.SignalR.ServiceEndpoint[]? ServiceEndpoints { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.Management.ServiceTransportType ServiceTransportType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.Runtime.CompilerServices.NullableContextAttribute((byte)1)]
        public void UseJsonObjectSerializer(Azure.Core.Serialization.ObjectSerializer objectSerializer) { }
    }
    public enum ServiceManagerRetryMode
    {
        Fixed = 0,
        Exponential = 1,
    }
    public partial class ServiceManagerRetryOptions
    {
        public ServiceManagerRetryOptions() { }
        public System.TimeSpan Delay { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.TimeSpan MaxDelay { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int MaxRetries { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.Management.ServiceManagerRetryMode Mode { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public enum ServiceTransportType
    {
        Transient = 0,
        Persistent = 1,
    }
    public abstract partial class StreamingManager : Microsoft.Azure.SignalR.Management.IStreamingManager
    {
        protected StreamingManager() { }
        public abstract System.Threading.Tasks.Task SendStreamAsync<TItem>(string connectionId, string streamId, System.Collections.Generic.IAsyncEnumerable<TItem> items, System.Threading.CancellationToken cancellationToken);
        public abstract System.Threading.Tasks.Task SendStreamAsync<TItem>(string connectionId, string streamId, System.Threading.Channels.ChannelReader<TItem> items, System.Threading.CancellationToken cancellationToken);
    }
    public abstract partial class UserGroupManager : Microsoft.Azure.SignalR.Management.IUserGroupManager
    {
        protected UserGroupManager() { }
        public abstract System.Threading.Tasks.Task AddToGroupAsync(string userId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task AddToGroupAsync(string userId, string groupName, System.TimeSpan ttl, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task<bool> IsUserInGroup(string userId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task RemoveFromAllGroupsAsync(string userId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public abstract System.Threading.Tasks.Task RemoveFromGroupAsync(string userId, string groupName, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
}
