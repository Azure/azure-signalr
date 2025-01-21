// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

[Serializable]
public class SkipTestException : Exception
{
    public readonly string? Reason;

    public SkipTestException(string? reason)
        : base("Test skipped. Reason: " + reason)
    {
        Reason = reason;
    }

    [Obsolete]
    protected SkipTestException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Reason = info.GetString(nameof(Reason));
    }

    [Obsolete]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        info.AddValue(nameof(Reason), Reason);

        base.GetObjectData(info, context);
    }
}
