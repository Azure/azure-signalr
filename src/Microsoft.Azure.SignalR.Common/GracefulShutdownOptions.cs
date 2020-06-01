using System;

namespace Microsoft.Azure.SignalR
{
    public class GracefulShutdownOptions
    {
        /// <summary>
        /// Specifies the timeout of a graceful shutdown process (in seconds). 
        /// Default value is 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = Constants.Periods.DefaultShutdownTimeout;

        /// <summary>
        /// Specifies if the client-connection assigned to this server can be migrated to another server.
        /// Default value is 0.
        /// 1: Migrate client-connection if the server was shutdown gracefully.
        /// </summary>
        public GracefulShutdownMode Mode { get; set; } = GracefulShutdownMode.Off;
    }
}
