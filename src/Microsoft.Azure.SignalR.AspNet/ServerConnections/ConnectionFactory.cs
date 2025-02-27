// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet;

#nullable enable

internal class ConnectionFactory : ConnectionFactoryBase
{
    public ConnectionFactory(IServerNameProvider nameProvider, ILoggerFactory loggerFactory) : base(nameProvider, loggerFactory)
    {
    }

    protected override void SetInternalUserAgent(IDictionary<string, string> headers)
    {
        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        headers[Constants.AsrsUserAgent] = ProductInfo.GetProductInfo();
    }
}
