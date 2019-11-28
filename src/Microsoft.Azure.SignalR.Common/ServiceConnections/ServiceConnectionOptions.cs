namespace Microsoft.Azure.SignalR
{
    class ServiceConnectionOptions
    {
        public ServiceConnectionType ConnectionType = ServiceConnectionType.Default;

        public ServiceConnectionMigrationLevel MigrationLevel = ServiceConnectionMigrationLevel.Off;

        public static ServiceConnectionOptions Default { get => new ServiceConnectionOptions(); }

        internal ServiceConnectionOptions()
        {

        }

        internal ServiceConnectionOptions Clone()
        {
            return new ServiceConnectionOptions
            {
                ConnectionType = ConnectionType,
                MigrationLevel = MigrationLevel
            };
        }
    }
}
