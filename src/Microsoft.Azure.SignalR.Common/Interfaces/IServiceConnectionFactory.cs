// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal interface IServiceConnectionFactory
{
    IServiceConnection Create(HubServiceEndpoint endpoint,
                              IServiceMessageHandler serviceMessageHandler,
                              AckHandler ackHandler,
                              ServiceConnectionType type);
}
