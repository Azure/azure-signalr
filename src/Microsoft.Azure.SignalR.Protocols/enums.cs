namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// The type of service event object.
    /// </summary>
    public enum ServiceEventObjectType
    {
        /// <summary>
        /// The instance of service.
        /// </summary>
        ServiceInstance = 0,
        /// <summary>
        /// The connection.
        /// </summary>
        Connection,
        /// <summary>
        /// The user.
        /// </summary>
        User,
        /// <summary>
        /// The group.
        /// </summary>
        Group,
        /// <summary>
        /// The server connection.
        /// </summary>
        ServerConnection,
    }

    /// <summary>
    /// The kind of service event.
    /// </summary>
    public enum ServiceEventKind
    {
        /// <summary>
        /// The reloading event.
        /// </summary>
        Reloading = 0,
        /// <summary>
        /// The format of id is invalid.
        /// </summary>
        Invalid,
        /// <summary>
        /// The id is not existed.
        /// </summary>
        NotExisted,
        /// <summary>
        /// The buffer-full event. When the server is sending too many messages at the same time, the service would back-pressure the messages to the server-side and also trigger this `BufferFull` event.
        /// </summary>
        BufferFull,
    }

    /// <summary>
    /// The type of data message.
    /// </summary>
    public enum DataMessageType
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// Handshake request or handshake response.
        /// </summary>
        Handshake = 1,
        /// <summary>
        /// Any kind of invocations.
        /// </summary>
        Invocation = 2,
        /// <summary>
        /// Others, include ping, ack, sequence.
        /// </summary>
        Other = 3,
    }
}
