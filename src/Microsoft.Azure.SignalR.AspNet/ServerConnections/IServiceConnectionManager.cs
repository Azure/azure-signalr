// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.AspNet;

internal interface IServiceConnectionManager : IServiceConnectionContainer
{
    void Initialize(IServiceConnectionContainerFactory connectionFactory);

    IServiceConnectionContainer WithHub(string hubName);
}