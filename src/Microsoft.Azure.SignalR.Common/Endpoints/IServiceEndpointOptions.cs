// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointOptions
    {
        ServiceEndpoint[] Endpoints { get; }
        string ApplicationName { get; }
        string ConnectionString { get; }
    }
}
