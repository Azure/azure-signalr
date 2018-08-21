// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IServiceConnectionManager : IServiceConnectionContainer
    {
        void Initialize(Func<string, IServiceConnection> connectionGenerator, int connectionCount);

        IServiceConnectionContainer WithHub(string hubName);

        /// <summary>
        ///  It is possible that the hub contains dot character, while the fully qualified name is formed as {HubName}.{Name} (Name can be connectionId or userId or groupId)
        ///  This method returns back all the possible combination of serviceConnection and {Name}
        /// </summary>
        /// <param name="nameWithHubPrefix">The fully qualified name</param>
        /// <returns>The combination of serviceConnection and name without hubname prefix</returns>
        IEnumerable<(IServiceConnectionContainer, string)> GetPossibleConnections(string nameWithHubPrefix);
    }
}