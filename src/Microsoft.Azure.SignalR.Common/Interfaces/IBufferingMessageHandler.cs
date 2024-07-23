// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR;

internal interface IBufferingMessageHandler
{
    List<IMemoryOwner<byte>> this[string index] { get; set; }

    bool TryGetValue(string connectionId, out List<IMemoryOwner<byte>> list);

    void Remove(string connectionId);
}
