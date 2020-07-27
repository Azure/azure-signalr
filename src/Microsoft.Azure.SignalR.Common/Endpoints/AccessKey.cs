// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Id { get; protected set; }

        public string Value { get; protected set; }

        public AccessKey(string key)
        {
            Id = key.GetHashCode().ToString();
            Value = key;
        }

        protected AccessKey() { }
    }
}