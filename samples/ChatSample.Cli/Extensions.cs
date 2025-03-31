// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ChatSample.Cli;

internal static class Extensions
{
    public static TBuilder When<TBuilder>(this TBuilder self, bool condition, Func<TBuilder, TBuilder> func)
    {
        return condition ? func(self) : self;
    }
}
