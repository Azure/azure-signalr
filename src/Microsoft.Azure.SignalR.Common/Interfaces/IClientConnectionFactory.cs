// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

internal interface IClientConnectionFactory
{
    IClientConnection CreateConnection(OpenConnectionMessage message, Action<HttpContext> configureContext = null);
}
