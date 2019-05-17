// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public interface ITestServer
    {
        string Start();
        void Stop();
    }
}