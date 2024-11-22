// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR;

internal static class Constants
{
    public const string AsrsMigrateFrom = "Asrs-Migrate-From";

    public const string AsrsMigrateTo = "Asrs-Migrate-To";

    public const string AsrsUserAgent = "Asrs-User-Agent";

    public const string AsrsInstanceId = "Asrs-Instance-Id";

    public const string AsrsIsDiagnosticClient = "Asrs-Is-Diagnostic-Client";

    public const string AsrsTokenIssuer = "azure-signalr";

    public const string AsrsDefaultScope = "https://signalr.azure.com/.default";


    public const int DefaultCloseTimeoutMilliseconds = 10000;

    public static class Keys
    {
        public const string AzureSignalRSectionKey = "Azure:SignalR";

        public const string ConnectionStringDefaultKey = $"{AzureSignalRSectionKey}:ConnectionString";

        public const string AzureSignalREnabledKey = $"{AzureSignalRSectionKey}:Enabled";

        public const string AzureSignalREndpointsKey = $"{AzureSignalRSectionKey}:Endpoints";

        public static readonly string ConnectionStringSecondaryKey =
            $"ConnectionStrings:{ConnectionStringDefaultKey}";
    }

    public static class Periods
    {
        // Custom handshake timeout of SignalR Service
        public const int DefaultHandshakeTimeout = 15;

        public const int MaxCustomHandshakeTimeout = 30;

        public static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromHours(1);

        public static readonly TimeSpan DefaultScaleTimeout = TimeSpan.FromMinutes(5);

        public static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(30);

        public static readonly TimeSpan RemoveFromServiceTimeout = TimeSpan.FromSeconds(5);

        public static readonly TimeSpan DefaultStatusPingInterval = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan DefaultServersPingInterval = TimeSpan.FromSeconds(5);

        // Depends on DefaultStatusPingInterval, make 1/2 to fast check.
        public static readonly TimeSpan DefaultCloseDelayInterval = TimeSpan.FromSeconds(5);
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

        public const string HttpTransportType = AzureSignalRSysPrefix + "htt";

        public const string CloseOnAuthExpiration = AzureSignalRSysPrefix + "coae";

        public const string AuthExpiresOn = AzureSignalRSysPrefix + "aeo";

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

        public const string RequestUICulture = "asrs_ui_lang";
    }

    public static class CustomizedPingTimer
    {
        public const string ServiceStatus = "ServiceStatus";

        public const string Servers = "Servers";
    }

    public static class Protocol
    {
        public const string Json = "json";

        public const string MessagePack = "messagepack";

        public const string BlazorPack = "blazorpack";
    }

    public static class Headers
    {
        public const string AsrsHeaderPrefix = "X-ASRS-";

        public const string AsrsServerId = AsrsHeaderPrefix + "Server-Id";

        public const string AsrsMessageTracingId = AsrsHeaderPrefix + "Message-Tracing-Id";

        public const string MicrosoftErrorCode = "x-ms-error-code";
    }

    public static class ErrorCodes
    {
        public const string WarningConnectionNotExisted = "Warning.Connection.NotExisted";

        public const string WarningUserNotExisted = "Warning.User.NotExisted";

        public const string WarningGroupNotExisted = "Warning.Group.NotExisted";

        public const string InfoUserNotInGroup = "Info.User.NotInGroup";

        public const string ErrorConnectionNotExisted = "Error.Connection.NotExisted";
    }

    public static class HttpClientNames
    {
        public const string Resilient = "Resilient";

        public const string MessageResilient = "MessageResilient";

        public const string UserDefault = "UserDefault";

        public const string InternalDefault = "InternalDefault";
    }
}