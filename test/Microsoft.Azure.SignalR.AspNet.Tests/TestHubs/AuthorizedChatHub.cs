// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Microsoft.Azure.SignalR.AspNet.Tests.TestHubs;

[Authorize, HubName("authchat")]
public class AuthorizedChatHub : Hub
{
}
