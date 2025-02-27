// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Extensions.Logging.Testing;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    // WriteContext, but with a timestamp...
    public class LogRecord
    {
        public DateTime Timestamp { get; }

        public WriteContext Write { get; }

        public LogRecord(DateTime timestamp, WriteContext write)
        {
            Timestamp = timestamp;
            Write = write;
        }
    }
}
