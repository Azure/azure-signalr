// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointGenerator
    {
        string GetClientAudience(string hubName);
        string GetClientEndpoint(string hubName, string originalPath);
        string GetServerAudience(string hubName);
        string GetServerEndpoint(string hubName);
    }
}
