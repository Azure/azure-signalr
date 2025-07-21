// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Azure;

using Microsoft.Azure.SignalR.Protocol;

#nullable enable

namespace Microsoft.Azure.SignalR;

internal class GroupMemberQueryResultPage : Page<GroupMember>
{
    private readonly IReadOnlyList<GroupMember> _value;
    private readonly string? _continuationToken;

    public GroupMemberQueryResultPage(IReadOnlyList<GroupMember> value, string? continuationToken)
    {
        _value = value;
        _continuationToken = continuationToken;
    }

    public override IReadOnlyList<GroupMember> Values => _value;

    public override string? ContinuationToken => _continuationToken;

    public override Response GetRawResponse()
    {
        // This class is for both WebSocket and REST API transports, therefore it does not have a raw response.
        // We can add it later for REST API if needed.
        throw new NotSupportedException();
    }
}
