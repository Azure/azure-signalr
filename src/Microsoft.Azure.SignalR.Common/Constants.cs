// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal static class Constants
    {
        public const string ServerStickyModeDefaultKey = "Azure:SignalR:ServerStickyMode";
        public const string ConnectionStringDefaultKey = "Azure:SignalR:ConnectionString";
        public const string ApplicationNameDefaultKey = "Azure:SignalR:ApplicationName";

        public const int DefaultShutdownTimeoutInSeconds = 30;
        public const int DefaultScaleTimeoutInMinutes = 15;

        public const string AsrsMigrateFrom = "Asrs-Migrate-From";
        public const string AsrsMigrateTo = "Asrs-Migrate-To";

        public const string AsrsUserAgent = "Asrs-User-Agent";
        public const string AsrsInstanceId = "Asrs-Instance-Id";

        public const string AzureSignalREnabledKey = "Azure:SignalR:Enabled";

        public static readonly string ConnectionStringSecondaryKey =
            $"ConnectionStrings:{ConnectionStringDefaultKey}";

        public static readonly string ConnectionStringKeyPrefix = $"{ConnectionStringDefaultKey}:";

        public static readonly string ApplicationNameDefaultKeyPrefix = $"{ApplicationNameDefaultKey}:";

        public static readonly string ConnectionStringSecondaryKeyPrefix = $"{ConnectionStringSecondaryKey}:";

        // Default access token lifetime
        public static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromHours(1);

        public static class ClaimType
        {
            public const string AzureSignalRSysPrefix = "asrs.s.";
            public const string AuthenticationType = AzureSignalRSysPrefix + "aut";
            public const string NameType = AzureSignalRSysPrefix + "nt";
            public const string RoleType = AzureSignalRSysPrefix + "rt";
            public const string UserId = AzureSignalRSysPrefix + "uid";
            public const string ServerName = AzureSignalRSysPrefix + "sn";
            public const string ServerStickyMode = AzureSignalRSysPrefix + "ssticky";
            public const string Id = AzureSignalRSysPrefix + "id";
            public const string AppName = AzureSignalRSysPrefix + "apn";
            public const string Version = AzureSignalRSysPrefix + "vn";
            public const string EnableDetailedErrors = AzureSignalRSysPrefix + "derror";
            public const string ServiceEndpointsCount = AzureSignalRSysPrefix + "secn";

            public const string AzureSignalRUserPrefix = "asrs.u.";
        }

        public static class Path
        {
            public const string Negotiate = "/negotiate";
        }

        public static class QueryParameter
        {
            public const string OriginalPath = "asrs.op";
            public const string ConnectionRequestId = "asrs_request_id";
        }
    }
}
