// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal enum ServiceConnectionMigrationLevel
    {
        /// <summary>
        /// 0, Default, client-connection will not be migrated at any time.
        /// </summary>
        Off = 0,
        /// <summary>
        /// 1, ShutdownOnly, client-connection will be migrated to another available server if a graceful shutdown had performed.
        /// </summary>
        ShutdownOnly = 1,
        /// <summary>
        /// 2, All, migration will happen even if one or all service-connection had been dropped accidentally. (pending messages will be lost)
        /// </summary>
        All = 2,
    }
}
