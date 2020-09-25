// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal interface IHttpUpstreamPropertiesFeature
    {
        string QueryString { get; }

        IReadOnlyList<string> ClaimStrings { get; }

        IReadOnlyList<string> GetSignatures(IReadOnlyList<string> keys);

        string Hub { get; }

        string UserIdentifier { get; }

        string ConnectionId { get; }
    }
}
