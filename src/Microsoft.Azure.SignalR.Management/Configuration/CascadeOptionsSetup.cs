// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management.Configuration
{
    /// <summary>
    /// Sets up TargetOptions from SourceOptions and tracks changes .
    /// </summary>
    internal abstract class CascadeOptionsSetup<SourceOptions, TargetOptions> : IConfigureOptions<TargetOptions>, IOptionsChangeTokenSource<TargetOptions>
        where SourceOptions : class
        where TargetOptions : class
    {
        private protected readonly IOptionsMonitor<SourceOptions> _monitor;
        private readonly IOptionsChangeTokenSource<SourceOptions> _changeTokenSource;
        private readonly IChangeToken _nullChangeToken;

        public CascadeOptionsSetup(IOptionsMonitor<SourceOptions> monitor, IOptionsChangeTokenSource<SourceOptions> changeTokenSource = null)
        //Making 'tokenSource' optional avoids error when 'tokenSource' is unavailable.
        {
            _monitor = monitor;
            _changeTokenSource = changeTokenSource;
            if (_changeTokenSource == null)
            {
                _nullChangeToken = NullChangeToken.Singleton;
            }
        }

        public string Name => Options.DefaultName;

        public abstract void Configure(TargetOptions options);

        public IChangeToken GetChangeToken()
        {
            if (_changeTokenSource != null)
            {
                return _changeTokenSource.GetChangeToken();
            }

            //until .NET 5, we can't return a ChangeToken as null.
            //fixes in https://github.com/dotnet/runtime/pull/43306
            return _nullChangeToken;
        }
    }
}