// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net.Http;

namespace Microsoft.Azure.SignalR.Common;

public static class Telemetry
{
    internal static ActivitySource ActivitySource = new ActivitySource("Azure.SignalR");

    public static Activity SendRequestEvent(HttpRequestMessage request)
    {
        var activity = ActivitySource.StartActivity($"SendRequest", ActivityKind.Client);
        activity?.SetTag("http.method", request.Method);
        activity?.SetTag("http.url", request.RequestUri);
        return activity;
    }
    
    public static Activity ReceiveResponseEvent(HttpResponseMessage response)
    {
        var activity = ActivitySource.StartActivity($"ReceiveResponse", ActivityKind.Server);
        activity?.SetTag("http.status_code", response.StatusCode);
        return activity;
    }
}