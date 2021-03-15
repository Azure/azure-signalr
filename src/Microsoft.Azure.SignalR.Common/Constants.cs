// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal static class Constants
    {
        public static class Keys
        {
            public const string AzureSignalRSectionKey = "Azure:SignalR";
            public const string ServerStickyModeDefaultKey = "Azure:SignalR:ServerStickyMode";
            public const string ConnectionStringDefaultKey = "Azure:SignalR:ConnectionString";
            public const string ApplicationNameDefaultKey = "Azure:SignalR:ApplicationName";
            public const string AzureSignalREnabledKey = "Azure:SignalR:Enabled";
            public const string AzureSignalREndpointsKey = "Azure:SignalR:Endpoints";

            public static readonly string ConnectionStringSecondaryKey =
                $"ConnectionStrings:{ConnectionStringDefaultKey}";
            public static readonly string ConnectionStringKeyPrefix = $"{ConnectionStringDefaultKey}:";
            public static readonly string ApplicationNameDefaultKeyPrefix = $"{ApplicationNameDefaultKey}:";
            public static readonly string ConnectionStringSecondaryKeyPrefix = $"{ConnectionStringSecondaryKey}:";
        }

        public const string AsrsMigrateFrom = "Asrs-Migrate-From";
        public const string AsrsMigrateTo = "Asrs-Migrate-To";

        public const string AsrsUserAgent = "Asrs-User-Agent";
        public const string AsrsInstanceId = "Asrs-Instance-Id";

        public const string AsrsIsDiagnosticClient = "Asrs-Is-Diagnostic-Client";

        public static class Periods
        {
            public static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromHours(1);
            public static readonly TimeSpan DefaultScaleTimeout = TimeSpan.FromMinutes(5);
            public static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(30);
            public static readonly TimeSpan RemoveFromServiceTimeout = TimeSpan.FromSeconds(5);

            public static readonly TimeSpan DefaultStatusPingInterval = TimeSpan.FromSeconds(10);
            public static readonly TimeSpan DefaultServersPingInterval = TimeSpan.FromSeconds(5);
            // Depends on DefaultStatusPingInterval, make 1/2 to fast check.
            public static readonly TimeSpan DefaultCloseDelayInterval = TimeSpan.FromSeconds(5);
            
            // Custom handshake timeout of SignalR Service
            public const int DefaultHandshakeTimeout = 15;
            public const int MaxCustomHandshakeTimeout = 30;
        }

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
            public const string MaxPollInterval = AzureSignalRSysPrefix + "ttl";
            public const string DiagnosticClient = AzureSignalRSysPrefix + "dc";
            public const string CustomHandshakeTimeout = AzureSignalRSysPrefix + "cht";

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
            public const string RequestCulture = "asrs_lang";
        }

        public static class CustomizedPingTimer
        {
            public const string ServiceStatus = "ServiceStatus";
            public const string Servers = "Servers";
        }

        public static class Protocol
        {
            public const string BlazorPack = "blazorpack";
        }
    }
}