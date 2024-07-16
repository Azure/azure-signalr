/*------------------------------------------------------------------------------
 * A simplified version of LogHelper
------------------------------------------------------------------------------*/

using System;
using System.Globalization;

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
