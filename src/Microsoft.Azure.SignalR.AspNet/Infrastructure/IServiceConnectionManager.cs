// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IServiceConnectionManager : IServiceConnectionContainer
    {
        void AddConnection(string hubName, IServiceConnectionContainer connection);

        IServiceConnectionContainer WithHub(string hubName);
    }
}