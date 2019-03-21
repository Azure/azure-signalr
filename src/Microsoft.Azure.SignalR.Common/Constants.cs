// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal static class Constants
    {
        public const string ConnectionStringDefaultKey = "Azure:SignalR:ConnectionString";
        public const string HubPrefixDefaultKey = "Azure:SignalR:HubPrefix";

        public static readonly string ConnectionStringSecondaryKey =
            $"ConnectionStrings:{ConnectionStringDefaultKey}";

        public static readonly string ConnectionStringKeyPrefix = $"{ConnectionStringDefaultKey}:";

        public static readonly string HubPrefixDefaultKeyPrefix = $"{HubPrefixDefaultKey}:";

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
            public const string Id = AzureSignalRSysPrefix + "id";
            public const string AppName = AzureSignalRSysPrefix + "apn";

            public const string AzureSignalRUserPrefix = "asrs.u.";
        }

        public static class Path
        {
            public const string Negotiate = "/negotiate";
        }

        public static class QueryParameter
        {
            public const string OriginalPath = "asrs.op";
        }

        public static class Config
        {
            public static readonly string ConnectionStringKey = "Azure:SignalR:ConnectionString";
        }
    }
}
