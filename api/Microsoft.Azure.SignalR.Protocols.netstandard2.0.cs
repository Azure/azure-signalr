namespace Microsoft.Azure.SignalR.Protocol
{
    public partial class AccessKeyRequestMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public AccessKeyRequestMessage() { }
        public AccessKeyRequestMessage(string token) { }
        public string? Kid { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? Token { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class AccessKeyResponseMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public AccessKeyResponseMessage() { }
        public AccessKeyResponseMessage(System.Exception e) { }
        public AccessKeyResponseMessage(string kid, string key) { }
        public string? AccessKey { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? ErrorMessage { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? ErrorType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? Kid { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class AckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public AckMessage(int ackId, int status) { }
        public AckMessage(int ackId, int status, string? message) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string Message { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Buffers.ReadOnlySequence<byte>? Payload { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int Status { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ArrayDictionary<TKey, TValue> : System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.Generic.IDictionary<TKey, TValue>, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>, System.Collections.IEnumerable where TKey : notnull
    {
        public static readonly Microsoft.Azure.SignalR.Protocol.ArrayDictionary<TKey, TValue> Empty;
        public ArrayDictionary(int capacity, System.Collections.Generic.IEqualityComparer<TKey>? comparer = null) { }
        public int Count { get { throw null; } }
        public bool IsReadOnly { get { throw null; } }
        public TValue this[TKey key] { get { throw null; } set { } }
        public System.Collections.Generic.ICollection<TKey> Keys { get { throw null; } }
        System.Collections.Generic.IEnumerable<TKey> System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>.Keys { get { throw null; } }
        System.Collections.Generic.IEnumerable<TValue> System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>.Values { get { throw null; } }
        public System.Collections.Generic.ICollection<TValue> Values { get { throw null; } }
        public void Add(System.Collections.Generic.KeyValuePair<TKey, TValue> item) { }
        public void Add(TKey key, TValue value) { }
        public void Clear() { }
        public bool Contains(System.Collections.Generic.KeyValuePair<TKey, TValue> item) { throw null; }
        public bool ContainsKey(TKey key) { throw null; }
        public void CopyTo(System.Collections.Generic.KeyValuePair<TKey, TValue>[] array, int arrayIndex) { }
        public Microsoft.Azure.SignalR.Protocol.ArrayDictionary<TKey, TValue>.Enumerator GetEnumerator() { throw null; }
        public bool Remove(System.Collections.Generic.KeyValuePair<TKey, TValue> item) { throw null; }
        public bool Remove(TKey key) { throw null; }
        System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<TKey, TValue>> System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<TKey, TValue>>.GetEnumerator() { throw null; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw null; }
        public bool TryGetValue(TKey key, out TValue value) { throw null; }
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public partial struct Enumerator : System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<TKey, TValue>>, System.Collections.IEnumerator, System.IDisposable
        {
            private object _dummy;
            private int _dummyPrimitive;
            public Enumerator(Microsoft.Azure.SignalR.Protocol.ArrayDictionary<TKey, TValue> dictionary) { throw null; }
            public System.Collections.Generic.KeyValuePair<TKey, TValue> Current { get { throw null; } }
            object System.Collections.IEnumerator.Current { get { throw null; } }
            public void Dispose() { }
            public bool MoveNext() { throw null; }
            public void Reset() { }
        }
    }
    public partial class BroadcastDataMessage : Microsoft.Azure.SignalR.Protocol.MulticastDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public BroadcastDataMessage(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public BroadcastDataMessage(System.Collections.Generic.IReadOnlyList<string>? excludedList, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public System.Collections.Generic.IReadOnlyList<string> ExcludedList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public partial class CheckConnectionExistenceWithAckMessage : Microsoft.Azure.SignalR.Protocol.CheckWithAckMessage
    {
        public CheckConnectionExistenceWithAckMessage(string connectionId, int ackId = 0, ulong? tracingId = default(ulong?)) : base (default(int), default(ulong?)) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class CheckGroupExistenceWithAckMessage : Microsoft.Azure.SignalR.Protocol.CheckWithAckMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public CheckGroupExistenceWithAckMessage(string groupName, int ackId = 0, ulong? tracingId = default(ulong?)) : base (default(int), default(ulong?)) { }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public partial class CheckUserExistenceWithAckMessage : Microsoft.Azure.SignalR.Protocol.CheckWithAckMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public CheckUserExistenceWithAckMessage(string userId, int ackId = 0, ulong? tracingId = default(ulong?)) : base (default(int), default(ulong?)) { }
        public byte PartitionKey { get { throw null; } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class CheckUserInGroupWithAckMessage : Microsoft.Azure.SignalR.Protocol.CheckWithAckMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public CheckUserInGroupWithAckMessage(string userId, string groupName, int ackId = 0, ulong? tracingId = default(ulong?)) : base (default(int), default(ulong?)) { }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class CheckWithAckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        protected CheckWithAckMessage(int ackId, ulong? tracingId) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ClientCompletionMessage : Microsoft.Azure.SignalR.Protocol.ServiceCompletionMessage, Microsoft.Azure.SignalR.Protocol.IHasProtocol
    {
        public ClientCompletionMessage(string invocationId, string connectionId, string callerServerId, string? protocol, System.ReadOnlyMemory<byte> payload, ulong? tracingId = default(ulong?)) : base (default(string), default(string), default(string), default(ulong?)) { }
        public System.Buffers.ReadOnlySequence<byte> Payload { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? Protocol { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ClientInvocationMessage : Microsoft.Azure.SignalR.Protocol.MultiPayloadDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public ClientInvocationMessage(string invocationId, string connectionId, string callerServerId, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public string CallerServerId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string InvocationId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public partial class CloseConnectionMessage : Microsoft.Azure.SignalR.Protocol.ConnectionMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        public CloseConnectionMessage(string connectionId) : base (default(string)) { }
        public CloseConnectionMessage(string connectionId, string? errorMessage, System.Collections.Generic.IDictionary<string, Microsoft.Extensions.Primitives.StringValues>? headers = null) : base (default(string)) { }
        public string ErrorMessage { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Extensions.Primitives.StringValues> Headers { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    [System.ComponentModel.BrowsableAttribute(false)]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    [System.ObsoleteAttribute("Please use CloseConnectionMessage")]
    public partial class CloseConnectionsWithAckMessage : Microsoft.Azure.SignalR.Protocol.CloseMultiConnectionsWithAckMessage
    {
        public CloseConnectionsWithAckMessage(int ackId) : base (default(int)) { }
    }
    [System.ComponentModel.BrowsableAttribute(false)]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
    [System.ObsoleteAttribute("Please use CloseConnectionMessage")]
    public partial class CloseConnectionWithAckMessage : Microsoft.Azure.SignalR.Protocol.CloseWithAckMessage
    {
        public CloseConnectionWithAckMessage(string connectionId, int ackId) : base (default(int)) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    public partial class CloseGroupConnectionsWithAckMessage : Microsoft.Azure.SignalR.Protocol.CloseMultiConnectionsWithAckMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public CloseGroupConnectionsWithAckMessage(string groupName, int ackId) : base (default(int)) { }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public abstract partial class CloseMultiConnectionsWithAckMessage : Microsoft.Azure.SignalR.Protocol.CloseWithAckMessage
    {
        public CloseMultiConnectionsWithAckMessage(int ackId) : base (default(int)) { }
        public System.Collections.Generic.IReadOnlyList<string> ExcludedList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class CloseUserConnectionsWithAckMessage : Microsoft.Azure.SignalR.Protocol.CloseMultiConnectionsWithAckMessage
    {
        public CloseUserConnectionsWithAckMessage(string userId, int ackId) : base (default(int)) { }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class CloseWithAckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        public CloseWithAckMessage(int ackId) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? Reason { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ConnectionDataMessage : Microsoft.Azure.SignalR.Protocol.ConnectionMessage, Microsoft.Azure.SignalR.Protocol.IHasDataMessageType, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartializable, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public ConnectionDataMessage(string connectionId, System.Buffers.ReadOnlySequence<byte> payload, ulong? tracingId = default(ulong?)) : base (default(string)) { }
        public ConnectionDataMessage(string connectionId, System.ReadOnlyMemory<byte> payload, ulong? tracingId = default(ulong?)) : base (default(string)) { }
        public bool IsPartial { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public System.Buffers.ReadOnlySequence<byte> Payload { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.Protocol.DataMessageType Type { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ConnectionFlowControlMessage : Microsoft.Azure.SignalR.Protocol.ConnectionMessage
    {
        public ConnectionFlowControlMessage(string connectionId, Microsoft.Azure.SignalR.Protocol.ConnectionFlowControlOperation op, Microsoft.Azure.SignalR.Protocol.ConnectionType type = Microsoft.Azure.SignalR.Protocol.ConnectionType.Client) : base (default(string)) { }
        public Microsoft.Azure.SignalR.Protocol.ConnectionType ConnectionType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public Microsoft.Azure.SignalR.Protocol.ConnectionFlowControlOperation Operation { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    public enum ConnectionFlowControlOperation
    {
        Pause = 1,
        PauseAck = 2,
        Resume = 3,
        Offline = 4,
    }
    public abstract partial class ConnectionMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        protected ConnectionMessage(string connectionId) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ConnectionReconnectMessage : Microsoft.Azure.SignalR.Protocol.ConnectionMessage
    {
        public ConnectionReconnectMessage(string connectionId) : base (default(string)) { }
    }
    public enum ConnectionType
    {
        Client = 1,
        Server = 2,
    }
    public enum DataMessageType
    {
        Unknown = 0,
        Handshake = 1,
        Invocation = 2,
        Other = 3,
        Close = 4,
    }
    public partial class ErrorCompletionMessage : Microsoft.Azure.SignalR.Protocol.ServiceCompletionMessage
    {
        public ErrorCompletionMessage(string invocationId, string connectionId, string callerServerId, string? error, ulong? tracingId = default(ulong?)) : base (default(string), default(string), default(string), default(ulong?)) { }
        public string Error { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class ExtensibleServiceMessage : Microsoft.Azure.SignalR.Protocol.ServiceMessage
    {
        protected ExtensibleServiceMessage() { }
    }
    public partial class GroupBroadcastDataMessage : Microsoft.Azure.SignalR.Protocol.MulticastDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public GroupBroadcastDataMessage(string groupName, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public GroupBroadcastDataMessage(string groupName, System.Collections.Generic.IReadOnlyList<string>? excludedList, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public string? CallerUserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Collections.Generic.IReadOnlyList<string> ExcludedList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Collections.Generic.IReadOnlyList<string> ExcludedUserList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public partial class GroupMember : System.IEquatable<Microsoft.Azure.SignalR.Protocol.GroupMember>
    {
        public GroupMember() { }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        protected GroupMember(Microsoft.Azure.SignalR.Protocol.GroupMember original) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        protected virtual System.Type EqualityContract { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        public string? UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public virtual bool Equals(Microsoft.Azure.SignalR.Protocol.GroupMember? other) { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public override bool Equals(object? obj) { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public override int GetHashCode() { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public static bool operator ==(Microsoft.Azure.SignalR.Protocol.GroupMember? left, Microsoft.Azure.SignalR.Protocol.GroupMember? right) { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public static bool operator !=(Microsoft.Azure.SignalR.Protocol.GroupMember? left, Microsoft.Azure.SignalR.Protocol.GroupMember? right) { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        protected virtual bool PrintMembers(System.Text.StringBuilder builder) { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public override string ToString() { throw null; }
        [System.Runtime.CompilerServices.CompilerGeneratedAttribute]
        public virtual Microsoft.Azure.SignalR.Protocol.GroupMember <Clone>$() { throw null; }
    }
    public partial class GroupMemberQueryMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        public GroupMemberQueryMessage() { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? ContinuationToken { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? MaxPageSize { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? Top { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public sealed partial class GroupMemberQueryResponse
    {
        public GroupMemberQueryResponse() { }
        public string? ContinuationToken { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Collections.Generic.IReadOnlyCollection<Microsoft.Azure.SignalR.Protocol.GroupMember> Members { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class HandshakeRequestMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public HandshakeRequestMessage(int version) { }
        public HandshakeRequestMessage(int version, int connectionType, int migrationLevel) { }
        public bool AllowStatefulReconnects { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int ConnectionType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int MigrationLevel { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? Target { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int Version { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class HandshakeResponseMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public HandshakeResponseMessage() { }
        public HandshakeResponseMessage(string? errorMessage) { }
        public string? ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? ErrorMessage { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial interface IAckableMessage
    {
        int AckId { get; set; }
    }
    public partial interface IHasDataMessageType
    {
        Microsoft.Azure.SignalR.Protocol.DataMessageType Type { get; set; }
    }
    public partial interface IHasProtocol
    {
        string? Protocol { get; set; }
    }
    public partial interface IHasSubscriberFilter
    {
        string? Filter { get; set; }
    }
    public partial interface IHasTtl
    {
        int? Ttl { get; set; }
    }
    public partial interface IMessageWithTracingId
    {
        ulong? TracingId { get; set; }
    }
    public partial interface IPartializable
    {
        bool IsPartial { get; set; }
    }
    public partial interface IPartitionableMessage
    {
        byte PartitionKey { get; }
    }
    public partial interface IServiceProtocol
    {
        int Version { get; }
        System.ReadOnlyMemory<byte> GetMessageBytes(Microsoft.Azure.SignalR.Protocol.ServiceMessage message);
        bool TryParseMessage(ref System.Buffers.ReadOnlySequence<byte> input, out Microsoft.Azure.SignalR.Protocol.ServiceMessage? message);
        void WriteMessage(Microsoft.Azure.SignalR.Protocol.ServiceMessage message, System.Buffers.IBufferWriter<byte> output);
    }
    public partial class JoinGroupMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public JoinGroupMessage(string connectionId, string groupName, ulong? tracingId = default(ulong?)) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class JoinGroupWithAckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public JoinGroupWithAckMessage(string connectionId, string groupName, int ackId, ulong? tracingId = default(ulong?)) { }
        public JoinGroupWithAckMessage(string connectionId, string groupName, ulong? tracingId = default(ulong?)) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class LeaveGroupMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public LeaveGroupMessage(string connectionId, string? groupName, ulong? tracingId = default(ulong?)) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class LeaveGroupWithAckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public LeaveGroupWithAckMessage(string connectionId, string? groupName, int ackId, ulong? tracingId = default(ulong?)) { }
        public LeaveGroupWithAckMessage(string connectionId, string? groupName, ulong? tracingId = default(ulong?)) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class MulticastDataMessage : Microsoft.Azure.SignalR.Protocol.MultiPayloadDataMessage, Microsoft.Azure.SignalR.Protocol.IHasSubscriberFilter
    {
        protected MulticastDataMessage(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public string? Filter { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class MultiConnectionDataMessage : Microsoft.Azure.SignalR.Protocol.MulticastDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public MultiConnectionDataMessage(System.Collections.Generic.IReadOnlyList<string> connectionList, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public System.Collections.Generic.IReadOnlyList<string> ConnectionList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public partial class MultiGroupBroadcastDataMessage : Microsoft.Azure.SignalR.Protocol.MulticastDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public MultiGroupBroadcastDataMessage(System.Collections.Generic.IReadOnlyList<string> groupList, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public System.Collections.Generic.IReadOnlyList<string> GroupList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
    }
    public abstract partial class MultiPayloadDataMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        protected MultiPayloadDataMessage(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) { }
        public System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> Payloads { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class MultiUserDataMessage : Microsoft.Azure.SignalR.Protocol.MulticastDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public MultiUserDataMessage(System.Collections.Generic.IReadOnlyList<string> userList, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public byte PartitionKey { get { throw null; } }
        public System.Collections.Generic.IReadOnlyList<string> UserList { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class OpenConnectionMessage : Microsoft.Azure.SignalR.Protocol.ConnectionMessage, Microsoft.Azure.SignalR.Protocol.IHasProtocol, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        public OpenConnectionMessage(string connectionId, System.Security.Claims.Claim[]? claims) : base (default(string)) { }
        public OpenConnectionMessage(string connectionId, System.Security.Claims.Claim[]? claims, System.Collections.Generic.IDictionary<string, Microsoft.Extensions.Primitives.StringValues> headers, string? queryString) : base (default(string)) { }
        public System.Security.Claims.Claim[] Claims { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Extensions.Primitives.StringValues> Headers { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? Protocol { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? QueryString { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class PingMessage : Microsoft.Azure.SignalR.Protocol.ServiceMessage
    {
        public static Microsoft.Azure.SignalR.Protocol.PingMessage Instance;
        public PingMessage() { }
        public string?[] Messages { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class RefreshAuthMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage
    {
        public RefreshAuthMessage(string connectionToken, System.Security.Claims.Claim[]? claims, System.DateTimeOffset expireTime, int ackId) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.Security.Claims.Claim[]? Claims { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string ConnectionToken { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public System.DateTimeOffset ExpireTime { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class ServiceCompletionMessage : Microsoft.Azure.SignalR.Protocol.ConnectionMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId
    {
        public ServiceCompletionMessage(string invocationId, string connectionId, string callerServerId, ulong? tracingId = default(ulong?)) : base (default(string)) { }
        public string CallerServerId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string InvocationId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class ServiceErrorMessage : Microsoft.Azure.SignalR.Protocol.ServiceMessage
    {
        public ServiceErrorMessage(string? errorMessage) { }
        public string ErrorMessage { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public enum ServiceEventKind
    {
        Reloading = 0,
        Invalid = 1,
        NotExisted = 2,
        BufferFull = 3,
    }
    public partial class ServiceEventMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public ServiceEventMessage(Microsoft.Azure.SignalR.Protocol.ServiceEventObjectType type, string? id, Microsoft.Azure.SignalR.Protocol.ServiceEventKind kind, string? message) { }
        public string? Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.Protocol.ServiceEventKind Kind { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string Message { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public Microsoft.Azure.SignalR.Protocol.ServiceEventObjectType Type { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public enum ServiceEventObjectType
    {
        ServiceInstance = 0,
        Connection = 1,
        User = 2,
        Group = 3,
        ServerConnection = 4,
    }
    public partial class ServiceMappingMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage
    {
        public ServiceMappingMessage(string invocationId, string connectionId, string instanceId) { }
        public string ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string InstanceId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string InvocationId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public abstract partial class ServiceMessage
    {
        protected ServiceMessage() { }
        public virtual Microsoft.Azure.SignalR.Protocol.ServiceMessage Clone() { throw null; }
        public static byte GeneratePartitionKey(string? input) { throw null; }
    }
    public partial class ServiceProtocol : Microsoft.Azure.SignalR.Protocol.IServiceProtocol
    {
        public ServiceProtocol() { }
        public int Version { get { throw null; } }
        public System.ReadOnlyMemory<byte> GetMessageBytes(Microsoft.Azure.SignalR.Protocol.ServiceMessage message) { throw null; }
        public T ParseMessagePayload<T>(System.Buffers.ReadOnlySequence<byte> input) where T : notnull, new() { throw null; }
        public bool TryParseMessage(ref System.Buffers.ReadOnlySequence<byte> input, out Microsoft.Azure.SignalR.Protocol.ServiceMessage? message) { throw null; }
        public void WriteMessage(Microsoft.Azure.SignalR.Protocol.ServiceMessage message, System.Buffers.IBufferWriter<byte> output) { }
        public void WriteMessagePayload<T>(T model, System.Buffers.IBufferWriter<byte> output) where T : notnull, new() { }
    }
    public static partial class ServiceProtocolConstants
    {
        public const int AccessKeyRequestType = 28;
        public const int AccessKeyResponseType = 29;
        public const int AckMessageType = 20;
        public const int BroadcastDataMessageType = 10;
        public const int CheckConnectionExistenceWithAckMessageType = 24;
        public const int CheckGroupExistenceWithAckMessageType = 23;
        public const int CheckUserExistenceWithAckMessageType = 25;
        public const int CheckUserInGroupWithAckMessageType = 21;
        public const int ClientCompletionMessageType = 35;
        public const int ClientInvocationMessageType = 34;
        public const int CloseConnectionMessageType = 5;
        public const int CloseConnectionsWithAckMessageType = 31;
        public const int CloseConnectionWithAckMessageType = 30;
        public const int CloseGroupConnectionsWithAckMessageType = 33;
        public const int CloseUserConnectionsWithAckMessageType = 32;
        public const int ConnectionDataMessageType = 6;
        public const int ConnectionFlowControlMessageType = 39;
        public const int ConnectionReconnectMessageType = 38;
        public const int ErrorCompletionMessageType = 36;
        public const int GroupBroadcastDataMessageType = 13;
        public const int GroupMemberQueryMessageType = 40;
        public const int HandshakeRequestType = 1;
        public const int HandshakeResponseType = 2;
        public const int JoinGroupMessageType = 11;
        public const int JoinGroupWithAckMessageType = 18;
        public const int LeaveGroupMessageType = 12;
        public const int LeaveGroupWithAckMessageType = 19;
        public const int MultiConnectionDataMessageType = 7;
        public const int MultiGroupBroadcastDataMessageType = 14;
        public const int MultiUserDataMessageType = 9;
        public const int OpenConnectionMessageType = 4;
        public const int PingMessageType = 3;
        public const int RefreshAuthMessageType = 41;
        public const int ServiceErrorMessageType = 15;
        public const int ServiceEventMessageType = 22;
        public const int ServiceMappingMessageType = 37;
        public const int UserDataMessageType = 8;
        public const int UserJoinGroupMessageType = 16;
        public const int UserJoinGroupWithAckMessageType = 26;
        public const int UserLeaveGroupMessageType = 17;
        public const int UserLeaveGroupWithAckMessageType = 27;
    }
    public partial class UserDataMessage : Microsoft.Azure.SignalR.Protocol.MulticastDataMessage, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public UserDataMessage(string userId, System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>> payloads, ulong? tracingId = default(ulong?)) : base (default(System.Collections.Generic.IDictionary<string, System.ReadOnlyMemory<byte>>), default(ulong?)) { }
        public byte PartitionKey { get { throw null; } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class UserJoinGroupMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IHasTtl, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public UserJoinGroupMessage(string userId, string groupName, ulong? tracingId = default(ulong?)) { }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? Ttl { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class UserJoinGroupWithAckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IHasTtl, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public UserJoinGroupWithAckMessage(string userId, string groupName, int ackId, int? ttl = default(int?), ulong? tracingId = default(ulong?)) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public int? Ttl { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class UserLeaveGroupMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public UserLeaveGroupMessage(string userId, string? groupName, ulong? tracingId = default(ulong?)) { }
        public string? GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
    public partial class UserLeaveGroupWithAckMessage : Microsoft.Azure.SignalR.Protocol.ExtensibleServiceMessage, Microsoft.Azure.SignalR.Protocol.IAckableMessage, Microsoft.Azure.SignalR.Protocol.IMessageWithTracingId, Microsoft.Azure.SignalR.Protocol.IPartitionableMessage
    {
        public UserLeaveGroupWithAckMessage(string userId, string? groupName, int ackId, ulong? tracingId = default(ulong?)) { }
        public int AckId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string? GroupName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public byte PartitionKey { get { throw null; } }
        public ulong? TracingId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute] set { } }
    }
}
