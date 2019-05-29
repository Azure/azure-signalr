// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public interface ITestClientSetFactory
    {
        ITestClientSet Create(string serverUrl, int count, ITestOutputHelper output);
    }
}
