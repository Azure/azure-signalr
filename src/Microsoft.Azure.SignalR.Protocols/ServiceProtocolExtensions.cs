// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
namespace Microsoft.Azure.SignalR.Protocol;

internal static class ServiceProtocolExtensions
{
    public static string ThrowWhenNull(this string? input)
    {
        return input ?? throw new ArgumentNullException(nameof(input));
    }
}

