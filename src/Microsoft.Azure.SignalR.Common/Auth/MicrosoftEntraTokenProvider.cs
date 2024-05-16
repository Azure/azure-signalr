// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class MicrosoftEntraTokenProvider : IAccessTokenProvider
    {
        private readonly AccessKeyForMicrosoftEntra _accessKey;

        public MicrosoftEntraTokenProvider(AccessKeyForMicrosoftEntra accessKey)
        {
            _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        }

        public Task<string> ProvideAsync() => _accessKey.GetMicrosoftEntraTokenAsync();
    }
}
