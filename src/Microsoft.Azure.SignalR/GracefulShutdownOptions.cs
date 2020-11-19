using System;

namespace Microsoft.Azure.SignalR
{
    public class GracefulShutdownOptions
    {
        /// <summary>
        /// Define the maximum waiting time to do the graceful shutdown process.
        /// </summary>
        public TimeSpan Timeout { get; set; } = Constants.Periods.DefaultShutdownTimeout;

        /// <summary>
        /// This mode defines the server's behavior after receiving a `Ctrl+C` (SIGINT).
        /// </summary>
        public GracefulShutdownMode Mode { get; set; } = GracefulShutdownMode.Off;
    }
}
