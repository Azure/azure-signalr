﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AadTokenProvider : IAccessTokenProvider
    {
        private readonly AadAccessKey _accessKey;

        public AadTokenProvider(AadAccessKey accessKey)
        {
            _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        }

        public Task<string> ProvideAsync() => _accessKey.GenerateAadTokenAsync();
    }
}
