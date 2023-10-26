// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.SignalR.Management;

internal class DummyBackOffPolicy : IBackOffPolicy
{
    public IEnumerable<TimeSpan> GetDelays() => Enumerable.Empty<TimeSpan>();
}
