// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;

using Microsoft.AspNetCore.SignalR.Protocol;

#nullable enable

namespace Microsoft.Azure.SignalR.Common;

internal interface IPayloadContentBuilder
{
    HttpContent? Build(HubMessage? payload, Type? typeHint);
}
