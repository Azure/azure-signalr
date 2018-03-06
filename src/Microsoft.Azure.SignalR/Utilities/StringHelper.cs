// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal static class StringHelper
    {
        internal static bool IgnoreCaseEquals(this string left, string right)
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
