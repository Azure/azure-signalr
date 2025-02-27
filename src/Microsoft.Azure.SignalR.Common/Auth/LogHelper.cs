// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*------------------------------------------------------------------------------
 * A simplified version of LogHelper
------------------------------------------------------------------------------*/

using System;
using System.Globalization;

namespace Microsoft.Azure.SignalR;

internal class LogHelper
{
    public static ArgumentNullException LogArgumentNullException(string name)
    {
        return new ArgumentNullException(name);
    }

    public static Exception LogExceptionMessage(Exception exception)
    {
        return exception;
    }

    public static string FormatInvariant(string format, params object[] args)
    {
        if (format == null)
            return string.Empty;

        if (args == null)
            return format;

        return string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
