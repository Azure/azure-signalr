﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerOptionsSetup : IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
    {
        private readonly IConfiguration _configuration;

        public ServiceManagerOptionsSetup(IConfiguration configuration = null)
        {
            _configuration = configuration;
        }

        public string Name => Options.DefaultName;

        public void Configure(ServiceManagerOptions options)
        {
            if (_configuration != null)
            {
                _configuration.GetSection(Constants.Keys.AzureSignalRSectionKey).Bind(options);

                //Our configuration format of service endoints array is not the standard format to configure array supported by .NET
                options.Endpoints = _configuration.GetSignalRServiceEndpoints(Constants.Keys.ConnectionStringDefaultKey);
            }
        }

        public IChangeToken GetChangeToken()
        {
            return _configuration?.GetReloadToken() ?? NullChangeToken.Singleton;
        }
    }
}