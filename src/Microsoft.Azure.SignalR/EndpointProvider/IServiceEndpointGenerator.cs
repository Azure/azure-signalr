// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal interface IServiceEndpointGenerator
{
    string GetClientAudience(string hubName, string applicationName);

    string GetClientEndpoint(string hubName, string applicationName, string originalPath, string queryString);

    string GetServerAudience(string hubName, string applicationName);

    string GetServerEndpoint(string hubName, string applicationName);
}
