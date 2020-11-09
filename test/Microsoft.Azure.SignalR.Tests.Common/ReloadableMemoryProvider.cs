// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class ReloadableMemoryProvider : ConfigurationProvider
    {
        public override void Set(string key, string value)
        {
            base.Set(key, value);
            OnReload();
        }
    }
}