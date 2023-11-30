// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    public class AzureSignalRRuntimeException : AzureSignalRException
    {
        private const string ErrorMessage = "Azure SignalR service runtime error.";

        public AzureSignalRRuntimeException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }
    }
}