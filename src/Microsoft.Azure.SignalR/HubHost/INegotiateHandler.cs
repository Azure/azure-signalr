// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface INegotiateHandler
    {
        Task<NegotiationResponse> Process(HttpContext context);
    }
}
