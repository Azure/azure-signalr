// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal interface IConnectionRequestIdProvider
{
    string GetRequestId(string traceIdentifier);
}

internal class DefaultConnectionRequestIdProvider : IConnectionRequestIdProvider
{
    public string GetRequestId(string traceIdentifier)
    {
        return AuthUtility.GenerateRequestId(traceIdentifier);
    }
}
