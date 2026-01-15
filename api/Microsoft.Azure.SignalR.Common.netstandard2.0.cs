namespace Microsoft.Azure.SignalR
{
    public enum AccessTokenAlgorithm
    {
        HS256 = 0,
        HS512 = 1,
    }
    public static partial class CancellationTokenExtensions
    {
        public static System.Threading.Tasks.Task AsTask(this System.Threading.CancellationToken cancellationToken) { throw null; }
        public static Microsoft.Azure.SignalR.CancellationTokenExtensions.CancellationTokenAwaiter GetAwaiter(this System.Threading.CancellationToken cancellationToken) { throw null; }
        public partial class CancellationTokenAwaiter : System.Runtime.CompilerServices.INotifyCompletion
        {
            public CancellationTokenAwaiter(System.Threading.CancellationToken cancellationToken) { }
            public bool IsCompleted { get { throw null; } }
            public void GetResult() { }
            public void OnCompleted(System.Action action) { }
        }
    }
    public partial class EndpointMetrics
    {
        public EndpointMetrics() { }
        public int ClientConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public int ConnectionCapacity { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public int ServerConnectionCount { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    public enum EndpointType
    {
        Primary = 0,
        Secondary = 1,
    }
    public enum GracefulShutdownMode
    {
        Off = 0,
        WaitForClientsClose = 1,
        MigrateClients = 2,
    }
    public partial interface IMessageRouter
    {
        System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForBroadcast(System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints);
        System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForConnection(string connectionId, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints);
        System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForGroup(string groupName, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints);
        System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> GetEndpointsForUser(string userId, System.Collections.Generic.IEnumerable<Microsoft.Azure.SignalR.ServiceEndpoint> endpoints);
    }
    public partial interface IServerNameProvider
    {
        string GetName();
    }
    public partial interface IServiceEventHandler
    {
        System.Threading.Tasks.Task HandleAsync(string connectionId, Microsoft.Azure.SignalR.Protocol.ServiceEventMessage message);
    }
    public enum ServerStickyMode
    {
        Disabled = 0,
        Preferred = 1,
        Required = 2,
    }
    public partial class ServiceEndpoint
    {
        public ServiceEndpoint(Microsoft.Azure.SignalR.ServiceEndpoint other) { }
        public ServiceEndpoint(string connectionString, Microsoft.Azure.SignalR.EndpointType type = Microsoft.Azure.SignalR.EndpointType.Primary, string name = "") { }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public ServiceEndpoint(string nameWithEndpointType, string connectionString) { }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public ServiceEndpoint(string nameWithEndpointType, System.Uri endpoint, Azure.Core.TokenCredential credential) { }
        public ServiceEndpoint(System.Uri endpoint, Azure.Core.TokenCredential credential, Microsoft.Azure.SignalR.EndpointType endpointType = Microsoft.Azure.SignalR.EndpointType.Primary, string name = "", System.Uri? serverEndpoint = null, System.Uri? clientEndpoint = null) { }
        public System.Uri ClientEndpoint { get { throw null; } set { } }
        public string? ConnectionString { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public string Endpoint { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public Microsoft.Azure.SignalR.EndpointMetrics EndpointMetrics { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public Microsoft.Azure.SignalR.EndpointType EndpointType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public bool IsActive { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public virtual string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public bool Online { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public System.Uri ServerEndpoint { get { throw null; } set { } }
        public override bool Equals(object? obj) { throw null; }
        public override int GetHashCode() { throw null; }
        public override string ToString() { throw null; }
    }
}
namespace Microsoft.Azure.SignalR.Common
{
    public partial class AccessKeyResponse
    {
        public AccessKeyResponse() { }
        public string AccessKey { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string KeyId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class AzureSignalRAccessTokenNotAuthorizedException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        [System.ObsoleteAttribute]
        public AzureSignalRAccessTokenNotAuthorizedException(string message) { }
        [System.ObsoleteAttribute]
        public AzureSignalRAccessTokenNotAuthorizedException(string credentialName, System.Exception inner) { }
    }
    public partial class AzureSignalRAccessTokenTooLongException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        public AzureSignalRAccessTokenTooLongException() { }
        protected AzureSignalRAccessTokenTooLongException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class AzureSignalRConfigurationNoEndpointException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        public AzureSignalRConfigurationNoEndpointException() { }
        protected AzureSignalRConfigurationNoEndpointException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class AzureSignalRException : System.Exception
    {
        public AzureSignalRException() { }
        protected AzureSignalRException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public AzureSignalRException(string message) { }
        public AzureSignalRException(string message, System.Exception ex) { }
    }
    public partial class AzureSignalRInaccessibleEndpointException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        protected AzureSignalRInaccessibleEndpointException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public AzureSignalRInaccessibleEndpointException(string requestUri, System.Exception innerException) { }
    }
    public partial class AzureSignalRInvalidArgumentException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        protected AzureSignalRInvalidArgumentException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public AzureSignalRInvalidArgumentException(string requestUri, System.Exception innerException, string detail) { }
    }
    public partial class AzureSignalRInvalidServiceOptionsException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        protected AzureSignalRInvalidServiceOptionsException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public AzureSignalRInvalidServiceOptionsException(string propertyName, string validScope) { }
    }
    public partial class AzureSignalRNoEndpointAvailableException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        public AzureSignalRNoEndpointAvailableException() { }
        protected AzureSignalRNoEndpointAvailableException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class AzureSignalRNoPrimaryEndpointException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        public AzureSignalRNoPrimaryEndpointException() { }
        protected AzureSignalRNoPrimaryEndpointException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class AzureSignalRNotConnectedException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        public AzureSignalRNotConnectedException() { }
        protected AzureSignalRNotConnectedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class AzureSignalRRuntimeException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        protected AzureSignalRRuntimeException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public AzureSignalRRuntimeException(string? requestUri, System.Exception inner) { }
    }
    public partial class AzureSignalRUnauthorizedException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        protected AzureSignalRUnauthorizedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        [System.ObsoleteAttribute]
        public AzureSignalRUnauthorizedException(string? requestUri, System.Exception innerException) { }
    }
    public partial class FailedWritingMessageToServiceException : Microsoft.Azure.SignalR.Common.ServiceConnectionNotActiveException
    {
        protected FailedWritingMessageToServiceException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public FailedWritingMessageToServiceException(string endpointUri) { }
        public string EndpointUri { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    public partial class ServiceConnectionNotActiveException : Microsoft.Azure.SignalR.Common.AzureSignalRException
    {
        public ServiceConnectionNotActiveException() { }
        protected ServiceConnectionNotActiveException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public ServiceConnectionNotActiveException(string message) { }
    }
}
