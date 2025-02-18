// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Tests.Common;

public interface IVerifiableLog : IDisposable
{
    LogRecord Expects(Func<LogRecord, bool> predicate);
    LogRecord Expects(string logEventName);
    IReadOnlyList<LogRecord> ExpectsMany(string logEventName);
    IReadOnlyList<LogRecord> ExpectsMany(Func<LogRecord, bool> predicate);
}
