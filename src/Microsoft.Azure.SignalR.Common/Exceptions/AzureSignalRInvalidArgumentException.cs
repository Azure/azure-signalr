// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    public class AzureSignalRInvalidArgumentException : AzureSignalRException
    {
        private const string ErrorMessage = "Invalid argument. Caused by one or more of the following reasons:";

        public AzureSignalRInvalidArgumentException(string requestUri, Exception innerException, string detail) : base(string.IsNullOrEmpty(requestUri) ? $"{ErrorMessage} {detail}" : $"{ErrorMessage} {detail} Request Uri: {requestUri}", innerException)
        {
        }
    }
}