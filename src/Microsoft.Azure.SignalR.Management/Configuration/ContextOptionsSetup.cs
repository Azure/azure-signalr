// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ContextOptionsSetup : IConfigureOptions<ContextOptions>, IOptionsChangeTokenSource<ContextOptions>
    {
        private readonly IConfiguration _configuration;

        public ContextOptionsSetup(IConfiguration configuration = null)
        {
            _configuration = configuration;
        }

        public string Name => Options.DefaultName;

        public void Configure(ContextOptions options)
        {
            if (_configuration != null)
            {
                _configuration.GetSection(Constants.Keys.AzureSignalRSectionKey).Bind(options);

                options.ServiceEndpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringDefaultKey, true).ToArray();
            }
        }

        public IChangeToken GetChangeToken()
        {
            return _configuration?.GetReloadToken() ?? NullChangeToken.Singleton;
        }
    }
}