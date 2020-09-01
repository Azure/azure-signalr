// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Emulator
{
    public interface IHttpUpstreamTrigger
    {
        Task<HttpResponseMessage> TriggerAsync(UpstreamContext context, IHttpUpstreamPropertiesFeature upstreamProperties, InvokeUpstreamParameters parameters, Action<HttpRequestMessage> configureRequest = null, CancellationToken token = default);
        bool TryGetMatchedUpstreamContext(InvokeUpstreamParameters parameters, out UpstreamContext upstreamContext);
    }
}
