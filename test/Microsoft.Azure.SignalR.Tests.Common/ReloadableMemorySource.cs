// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class ReloadableMemorySource : IConfigurationSource
    {
        private readonly ReloadableMemoryProvider _provider;

        public ReloadableMemorySource(ReloadableMemoryProvider provider) => _provider = provider;

        public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;
    }
}