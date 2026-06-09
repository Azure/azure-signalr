// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IPartializable
    {
        bool IsPartial { get; set; }
    }
}
