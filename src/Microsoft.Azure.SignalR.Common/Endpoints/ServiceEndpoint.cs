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
        public string ConnectionString { get; }

        public string Key { get; }

        internal string Endpoint { get; }

        internal string Version { get; }

        internal string AccessKey { get; }

        internal int? Port { get; }

        public ServiceEndpoint(string connectionString) : this(Constants.Config.ConnectionStringKey, connectionString)
        {
        }

        public ServiceEndpoint(string key, string connectionString)
        {
            // The provider is responsible to check if the connection string is empty and throw correct error message
            if (!string.IsNullOrEmpty(connectionString))
            {
                (Endpoint, AccessKey, Version, Port) = ConnectionStringParser.Parse(connectionString);
            }

            Key = key;
            ConnectionString = connectionString;
        }

        public override int GetHashCode()
        {
            // cares only about key
            return Key.GetHashCode();
        }
    }
}
