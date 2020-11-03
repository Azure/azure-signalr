// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management.Configuration
{
    /// <summary>
    /// Sets up TargetOptions from ServiceManagerOptions and tracks changes .
    /// </summary>
    internal abstract class CascadeOptionsSetup<TargetOptions> : IConfigureOptions<TargetOptions>, IOptionsChangeTokenSource<TargetOptions>
        where TargetOptions : class
    {
        private readonly ServiceManagerOptions _initialSource;
        private readonly IConfiguration _configuration;

        //Making 'configuration' optional avoids error when 'tokenSource' is unavailable.
        public CascadeOptionsSetup(IOptions<ServiceManagerOptions> initialSource, IConfiguration configuration = null)
        {
            _initialSource = initialSource.Value;
            _configuration = configuration;
        }

        public string Name => Options.DefaultName;

        public void Configure(TargetOptions target)
        {
            if (_configuration == null)
            {
                Convert(target, _initialSource);
            }
            else
            {
                var sourceOption = _configuration.GetSection(ServiceManagerOptions.Section).Get<ServiceManagerOptions>();
                Convert(target, sourceOption);
            }
        }

        protected abstract void Convert(TargetOptions target, ServiceManagerOptions source);

        public IChangeToken GetChangeToken() => _configuration?.GetReloadToken() ?? NullChangeToken.Singleton;

    }
}