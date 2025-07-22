// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using Azure;

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class PagenableGroupMember : AsyncPageable<GroupMember>
{
    // (string? continuationToken, int? pageSizeHint) => IAsyncEnumerable<Page<GroupMember>>
    private readonly Func<string?, int?, IAsyncEnumerable<Page<GroupMember>>> _fetchPages;

    public PagenableGroupMember(Func<string?, int?, IAsyncEnumerable<Page<GroupMember>>> fetchPages, CancellationToken cancellationToken = default): base(cancellationToken)
    {
        _fetchPages = fetchPages;
    }

    public override IAsyncEnumerable<Page<GroupMember>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
    {
        return _fetchPages(continuationToken, pageSizeHint);
    }
}
