// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal static class Constants
    {
        public static class ClaimType
        {
            public const string AzureSignalRSysPrefix = "azure.signalr.sys.";
            public const string AuthenticationType = AzureSignalRSysPrefix + "authenticationtype";
            public const string UserId = AzureSignalRSysPrefix + "userid";
        }

        public static class Path
        {
            public const string Negotiate = "/negotiate";
        }

        public static class Config
        {
            public static readonly string ConnectionStringKey = "Azure:SignalR:ConnectionString";
        }
    }
}
