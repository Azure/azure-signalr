// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal class ConnectionMigrationFeature : IConnectionMigrationFeature
{
    public string MigrateTo { get; private set; }

    public string MigrateFrom { get; private set; }

    public ConnectionMigrationFeature(string from, string to)
    {
        MigrateFrom = from;
        MigrateTo = to;
    }
}
