// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management.Configuration
{
    /// <summary>
    /// Sets up TargetOptions from SourceOptions and tracks changes .
    /// </summary>
    internal abstract class CascadeOptionsSetup<TargetOptions, SourceOptions> : IConfigureOptions<TargetOptions>, IOptionsChangeTokenSource<TargetOptions>, IDisposable
        where SourceOptions : class
        where TargetOptions : class
    {
        private readonly IDisposable _registration;
        private readonly IOptionsMonitor<SourceOptions> _sourceMonitor;
        private ConfigurationReloadToken _changeToken;

        public CascadeOptionsSetup(IOptionsMonitor<SourceOptions> sourceMonitor)
        {
            _registration = sourceMonitor.OnChange(RaiseChange);
            _changeToken = new ConfigurationReloadToken();
            _sourceMonitor = sourceMonitor;
        }

        public string Name => Options.DefaultName;

        public void Configure(TargetOptions target) => Convert(target, _sourceMonitor.CurrentValue);

        protected abstract void Convert(TargetOptions target, SourceOptions source);

        public IChangeToken GetChangeToken() => _changeToken;

        private void RaiseChange(SourceOptions sourceOptions)
        {
            var previousToken = Interlocked.Exchange(ref _changeToken, new ConfigurationReloadToken());
            previousToken.OnReload();
        }

        public void Dispose()
        {
            _registration.Dispose();
        }
    }
}