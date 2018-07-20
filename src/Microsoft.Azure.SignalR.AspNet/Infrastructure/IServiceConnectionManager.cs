// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IServiceConnectionManager : IServiceConnection
    {
        void AddConnection(string hubName, IServiceConnection connection);

        IServiceConnection WithHub(string hubName);
    }
}