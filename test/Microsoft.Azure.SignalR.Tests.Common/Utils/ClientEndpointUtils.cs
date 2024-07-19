// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Tests.Common;

public static class ClientEndpointUtils
{
    private const string Endpoint = "https://abc";

    public static string GetExpectedClientEndpoint(string hubName, string appName = null, string endpoint = Endpoint)
    {
        if (string.IsNullOrEmpty(appName))
        {
            return $"{endpoint}/client/?hub={hubName.ToLower()}";
        }

        return $"{endpoint}/client/?hub={appName.ToLower()}_{hubName.ToLower()}";
    }
}