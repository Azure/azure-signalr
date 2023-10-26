// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal interface IBackOffPolicy
{
    IEnumerable<TimeSpan> GetDelays();
}
