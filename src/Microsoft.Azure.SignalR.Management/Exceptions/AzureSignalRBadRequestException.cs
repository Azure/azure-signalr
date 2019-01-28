// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class AzureSignalRBadRequestException : AzureSignalRException
    {
        private const string ErrorMessage = "Bad Request. Caused by one or more of the following reasons:";

        public AzureSignalRBadRequestException(string requestUri, Exception innerException, string detail) : base(String.IsNullOrEmpty(requestUri) ? $"{ErrorMessage} {detail}" : $"{ErrorMessage} {detail} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRBadRequestException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}