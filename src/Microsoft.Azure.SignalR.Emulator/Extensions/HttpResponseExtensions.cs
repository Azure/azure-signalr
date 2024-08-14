// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal static class HttpResponseExtensions
{
    public static bool IsSuccessStatusCode(this HttpResponse response) =>
        response.StatusCode >= 200 && response.StatusCode <= 299;

    public static void SetMsErrorCodeHeader(this HttpResponse response, string code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            const string MSErrorCodeKey = "x-ms-error-code";
            if (!response.Headers.ContainsKey(MSErrorCodeKey))
            {
                response.Headers[MSErrorCodeKey] = new StringValues(code);
            }
        }
    }
}