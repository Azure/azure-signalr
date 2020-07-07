// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Value { get; }
        public string Id { get; }

        public Task InitializedTask => Task.CompletedTask;

        public AccessKey(string key)
        {
            Value = key;
            Id = key.GetHashCode().ToString();
        }
    }
}
