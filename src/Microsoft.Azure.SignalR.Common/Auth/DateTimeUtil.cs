/*------------------------------------------------------------------------------
 * Copied from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/Microsoft.IdentityModel.Tokens/DateTimeUtil.cs
------------------------------------------------------------------------------*/

using System;

namespace Microsoft.Azure.SignalR
{
    internal static class DateTimeUtil
    {
        /// <summary>
        /// Add a DateTime and a TimeSpan.
        /// The maximum time is DateTime.MaxTime.  It is not an error if time + timespan > MaxTime.
        /// Just return MaxTime.
        /// </summary>
        /// <param name="time">Initial <see cref="DateTime"/> value.</param>
        /// <param name="timespan"><see cref="TimeSpan"/> to add.</param>
        /// <returns><see cref="DateTime"/> as the sum of time and timespan.</returns>
        public static DateTime Add(DateTime time, TimeSpan timespan)
        {
            if (timespan == TimeSpan.Zero)
            {
                return time;
            }

            if (timespan > TimeSpan.Zero && DateTime.MaxValue - time <= timespan)
            {
                return GetMaxValue(time.Kind);
            }

            if (timespan < TimeSpan.Zero && DateTime.MinValue - time >= timespan)
            {
                return GetMinValue(time.Kind);
            }

            return time + timespan;
        }

        /// <summary>
        /// Gets the Maximum value for a DateTime specifying kind.
        /// </summary>
        /// <param name="kind">DateTimeKind to use.</param>
        /// <returns>DateTime of specified kind.</returns>
        public static DateTime GetMaxValue(DateTimeKind kind)
        {
            if (kind == DateTimeKind.Unspecified)
                return new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc);

            return new DateTime(DateTime.MaxValue.Ticks, kind);
        }

        /// <summary>
        /// Gets the Minimum value for a DateTime specifying kind.
        /// </summary>
        /// <param name="kind">DateTimeKind to use.</param>
        /// <returns>DateTime of specified kind.</returns>
        public static DateTime GetMinValue(DateTimeKind kind)
        {
            if (kind == DateTimeKind.Unspecified)
                return new DateTime(DateTime.MinValue.Ticks, DateTimeKind.Utc);

            return new DateTime(DateTime.MinValue.Ticks, kind);
        }

        /// <summary>
        /// Ensures that DataTime is UTC.
        /// </summary>
        /// <param name="value"><see cref="DateTime"/>to convert.</param>
        /// <returns></returns>
        public static DateTime? ToUniversalTime(DateTime? value)
        {
            if (value == null || value.Value.Kind == DateTimeKind.Utc)
                return value;

            return ToUniversalTime(value.Value);
        }

        /// <summary>
        /// Ensures that DateTime is UTC.
        /// </summary>
        /// <param name="value"><see cref="DateTime"/>to convert.</param>
        /// <returns></returns>
        public static DateTime ToUniversalTime(DateTime value)
        {

            if (value.Kind == DateTimeKind.Utc)
                return value;

            return value.ToUniversalTime();
        }
    }
}
