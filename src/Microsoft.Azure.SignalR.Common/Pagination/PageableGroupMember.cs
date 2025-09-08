// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using Azure;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class PageableGroupMember : AsyncPageable<SignalRGroupConnection>
{
    // (string? continuationToken, int? pageSizeHint) => IAsyncEnumerable<Page<GroupMember>>
    private readonly Func<string?, int?, IAsyncEnumerable<Page<SignalRGroupConnection>>> _fetchPages;

    public PageableGroupMember(Func<string?, int?, IAsyncEnumerable<Page<SignalRGroupConnection>>> fetchPages, CancellationToken cancellationToken = default) : base(cancellationToken)
    {
        _fetchPages = fetchPages;
    }

    public override IAsyncEnumerable<Page<SignalRGroupConnection>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
    {
        return _fetchPages(continuationToken, pageSizeHint);
    }
}
