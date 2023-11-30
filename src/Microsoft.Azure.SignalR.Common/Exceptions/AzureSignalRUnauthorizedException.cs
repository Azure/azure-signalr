﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    public class AzureSignalRUnauthorizedException : AzureSignalRException
    {
        private const string ErrorMessage = "Authorization failed. If you were using AccessKey, please check connection string and see if the AccessKey is correct. If you were using Azure Active Directory, please note that the role assignments will take up to 30 minutes to take effect if it was added recently.";

        public AzureSignalRUnauthorizedException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

        internal AzureSignalRUnauthorizedException(Exception innerException) : base(ErrorMessage, innerException)
        {
        }
    }
}