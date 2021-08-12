﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRUnauthorizedException : AzureSignalRException
    {
        private const string ErrorMessage = "Authorization failed. Please check the connection string and role assignments. The role assignments will take up to 30 minutes to take effect if it was added recently.";

        public AzureSignalRUnauthorizedException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

        internal AzureSignalRUnauthorizedException(Exception innerException) : base(ErrorMessage, innerException)
        {
        }

        protected AzureSignalRUnauthorizedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}