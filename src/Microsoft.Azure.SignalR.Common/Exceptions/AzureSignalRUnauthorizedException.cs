// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRUnauthorizedException : AzureSignalRException
    {
        private const string ErrorMessage = "Authorization failed. If you were using AccessKey, please check connection string and see if the AccessKey is correct. If you were using Azure Active Directory, please note that the role assignments will take up to 30 minutes to take effect if it was added recently.";

        private const string ErrorMessageLocal = "Authorization failed. Please check your connection string and see if the AccessKey is correct.";

        private const string ErrorMessageAad = "Authorization failed. Please check your role assignments. Note: New role assignments will take up to 30 minutes to take effect.";

        [Obsolete]
        public AzureSignalRUnauthorizedException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

        private AzureSignalRUnauthorizedException(Exception ex, string message) : base(message, ex)
        {
        }

        internal static AzureSignalRUnauthorizedException Wraps(AuthType authType, Exception innerException) => authType switch
        {
            AuthType.Local => new AzureSignalRUnauthorizedException(innerException, ErrorMessageLocal),
            AuthType.AzureAD => new AzureSignalRUnauthorizedException(innerException, ErrorMessageAad),
            _ => throw new NotSupportedException(),
        };

        internal static AzureSignalRUnauthorizedException Wraps(AuthType authType, Uri requestUri, Exception innerException) => authType switch
        {
            AuthType.Local => new AzureSignalRUnauthorizedException(innerException, $"{ErrorMessageLocal} Request Uri: {requestUri}"),
            AuthType.AzureAD => new AzureSignalRUnauthorizedException(innerException, $"{ErrorMessageAad} Request Uri: {requestUri}"),
            _ => throw new NotSupportedException(),
        };
    }
}