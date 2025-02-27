namespace Microsoft.Azure.SignalR.Serverless.Protocols
{
    public partial class CloseConnectionMessage : Microsoft.Azure.SignalR.Serverless.Protocols.ServerlessMessage
    {
        public CloseConnectionMessage() { }
        [Newtonsoft.Json.JsonPropertyAttribute(PropertyName="error")]
        public string? Error { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class InvocationMessage : Microsoft.Azure.SignalR.Serverless.Protocols.ServerlessMessage
    {
        public InvocationMessage() { }
        [Newtonsoft.Json.JsonPropertyAttribute(PropertyName="arguments")]
        public object?[]? Arguments { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [Newtonsoft.Json.JsonPropertyAttribute(PropertyName="invocationId")]
        public string? InvocationId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [Newtonsoft.Json.JsonPropertyAttribute(PropertyName="target")]
        public string? Target { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial interface IServerlessProtocol
    {
        int Version { get; }
        bool TryParseMessage(ref System.Buffers.ReadOnlySequence<byte> input, out Microsoft.Azure.SignalR.Serverless.Protocols.ServerlessMessage? message);
    }
    public partial class JsonServerlessProtocol : Microsoft.Azure.SignalR.Serverless.Protocols.IServerlessProtocol
    {
        public JsonServerlessProtocol() { }
        public int Version { get { throw null; } }
        public bool TryParseMessage(ref System.Buffers.ReadOnlySequence<byte> input, out Microsoft.Azure.SignalR.Serverless.Protocols.ServerlessMessage? message) { throw null; }
    }
    public partial class MessagePackServerlessProtocol : Microsoft.Azure.SignalR.Serverless.Protocols.IServerlessProtocol
    {
        public MessagePackServerlessProtocol() { }
        public int Version { get { throw null; } }
        public bool TryParseMessage(ref System.Buffers.ReadOnlySequence<byte> input, out Microsoft.Azure.SignalR.Serverless.Protocols.ServerlessMessage? message) { throw null; }
    }
    public partial class OpenConnectionMessage : Microsoft.Azure.SignalR.Serverless.Protocols.ServerlessMessage
    {
        public OpenConnectionMessage() { }
    }
    public abstract partial class ServerlessMessage
    {
        protected ServerlessMessage() { }
        [Newtonsoft.Json.JsonPropertyAttribute(PropertyName="type")]
        public int Type { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
}
