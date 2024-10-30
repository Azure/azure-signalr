// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR;

internal class ParsedConnectionString
{
    internal IAccessKey AccessKey { get; set; }

    internal Uri Endpoint { get; set; }

    internal Uri ClientEndpoint { get; set; }

    internal Uri ServerEndpoint { get; set; }

    internal string Version { get; set; }

}
