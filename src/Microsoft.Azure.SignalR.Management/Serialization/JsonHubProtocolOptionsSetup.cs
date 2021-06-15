// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NETSTANDARD2_0
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class JsonHubProtocolOptionsSetup : IConfigureOptions<JsonHubProtocolOptions>
    {
        private readonly IOptions<NewtonsoftServiceHubProtocolOptions> _srcOptions;

        public JsonHubProtocolOptionsSetup(IOptions<NewtonsoftServiceHubProtocolOptions> srcOptions)
        {
            _srcOptions = srcOptions;
        }

        public void Configure(JsonHubProtocolOptions options)
        {
            options.PayloadSerializerSettings = _srcOptions.Value.PayloadSerializerSettings;
        }
    }
}
#endif