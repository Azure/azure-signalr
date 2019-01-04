// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// TODO: expose it to the customer
    /// </summary>
    internal class ServiceEndpoint
    {
        internal static readonly ServiceEndpoint Empty = new ServiceEndpoint();

        private static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromHours(1);

        public string ConnectionString { get; }

        public string Key { get; }

        public TimeSpan AccessTokenLifetime { get; }

        internal string Endpoint { get; }

        internal string Version { get; }

        internal string AccessKey { get; }

        internal int? Port { get; }

        // for test purpose
        internal ServiceEndpoint() { }

        public ServiceEndpoint(string connectionString) : this(Constants.Config.ConnectionStringKey, connectionString)
        {
        }

        public ServiceEndpoint(string key, string connectionString, TimeSpan? ttl = null)
        {
            // The provider is responsible to check if the connection string is empty and throw correct error message
            if (!string.IsNullOrEmpty(connectionString))
            {
                (Endpoint, AccessKey, Version, Port) = ConnectionStringParser.Parse(connectionString);
            }

            Key = key;
            ConnectionString = connectionString;
            AccessTokenLifetime = ttl ?? DefaultAccessTokenLifetime;
        }

        public override int GetHashCode()
        {
            // cares only about key
            return Key.GetHashCode();
        }
    }
}
