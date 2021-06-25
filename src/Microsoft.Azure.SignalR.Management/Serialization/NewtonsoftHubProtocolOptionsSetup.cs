// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_0_OR_GREATER
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// An options setup class used to convert our own <see cref="NewtonsoftServiceHubProtocolOptions"/> to Asp.Net Core <see cref="NewtonsoftJsonHubProtocolOptions"/>
    /// </summary>
    internal class NewtonsoftHubProtocolOptionsSetup : IConfigureOptions<NewtonsoftJsonHubProtocolOptions>
    {
        private readonly IOptions<NewtonsoftServiceHubProtocolOptions> _srcOptions;

        public NewtonsoftHubProtocolOptionsSetup(IOptions<NewtonsoftServiceHubProtocolOptions> options)
        {
            _srcOptions = options;
        }

        public void Configure(NewtonsoftJsonHubProtocolOptions options)
        {
            options.PayloadSerializerSettings = _srcOptions.Value.PayloadSerializerSettings;
        }
    }
}
#endif