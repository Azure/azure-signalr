// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.SignalR
{
    public class HubMapping
    {
        public HubMapping(Type hubType, PathString path)
        {
            HubType = hubType;
            Path = path;
        }

        public PathString Path { get; set; }

        public Type HubType { get; set; }
    }
}
