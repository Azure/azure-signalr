// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class ConnectionFactory : ConnectionFactoryBase
{
    private readonly IOptions<ServiceOptions> _options;

    public ConnectionFactory(IServerNameProvider nameProvider,
                             IOptions<ServiceOptions> options,
                             ILoggerFactory loggerFactory) : base(nameProvider, loggerFactory)
    {
        _options = options;
    }

    protected override void SetCustomHeaders(IDictionary<string, string> headers)
    {
        _options.Value.CustomHeaderProvider?.Invoke(headers);
    }
}
