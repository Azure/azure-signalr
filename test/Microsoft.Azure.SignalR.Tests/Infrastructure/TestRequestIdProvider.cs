// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Tests;

public class TestRequestIdProvider : IConnectionRequestIdProvider
{
    private readonly string _id;

    public TestRequestIdProvider(string id)
    {
        _id = id;
    }

    public string GetRequestId(string clientRequestId)
    {
        return _id;
    }
}
