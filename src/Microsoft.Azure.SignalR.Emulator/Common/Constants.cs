﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common
{
    internal class Constants
    {
        public const string Asterisk = "*";

        public static class ContentTypes
        {
            // Serverless Http Request
            public const string JsonContentType = "application/json";
            public const string MessagePackContentType = "application/x-msgpack";
            public const string BinaryContentType = "application/octet-stream";
            public const string PlainTextContentType = "text/plain";
        }

        public static class ClaimTypes
        {
            public const string AzureSignalRSysPrefix = "asrs.s.";

            public const string UserIdClaimType = AzureSignalRSysPrefix + "uid";
        }

        public static class Headers
        {
            public const string AsrsHeaderPrefix = "X-ASRS-";
            public const string AsrsConnectionIdHeader = AsrsHeaderPrefix + "Connection-Id";
            public const string AsrsUserClaims = AsrsHeaderPrefix + "User-Claims";
            public const string AsrsUserId = AsrsHeaderPrefix + "User-Id";
            public const string AsrsHubNameHeader = AsrsHeaderPrefix + "Hub";
            public const string AsrsCategory = AsrsHeaderPrefix + "Category";
            public const string AsrsEvent = AsrsHeaderPrefix + "Event";
            public const string AsrsClientQueryString = AsrsHeaderPrefix + "Client-Query";
            public const string AsrsSignature = AsrsHeaderPrefix + "Signature";
            public const string AsrsClientCertThumbprint = AsrsHeaderPrefix + "Client-Cert-Thumbprint";
            public const string AsrsConnectionGroups = AsrsHeaderPrefix + "Connection-Group";
        }
    }
}
