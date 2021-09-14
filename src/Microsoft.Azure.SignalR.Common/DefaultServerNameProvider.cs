// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultServerNameProvider : IServerNameProvider
    {
        private readonly string _name = $"{Environment.MachineName}_{Guid.NewGuid():N}";

        public string GetName()
        {
            return _name;
        }
    }
}
