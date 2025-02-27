// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal class ManagementConnectionFactory(IOptions<ServiceManagerOptions> context,
                                           IServerNameProvider serverNameProvider,
                                           ILoggerFactory loggerFactory)
    : ConnectionFactoryBase(serverNameProvider, loggerFactory)
{
    private readonly string? _productInfo = context.Value.ProductInfo;

    internal override void SetInternalHeaders(IDictionary<string, string> headers)
    {
        base.SetInternalHeaders(headers);

        if (_productInfo != null)
        {
            headers[Constants.AsrsUserAgent] = _productInfo;
        }
    }

    protected override void SetCustomHeaders(IDictionary<string, string> headers)
    {
        return;
    }
}
