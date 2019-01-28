// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRUnableToAccessException : AzureSignalRException
    {
        private const string ErrorMessage = "Unable to access SignalR service. May caused by one or more of the following reasons: Incorrect endpoint or DNS error.";

        public AzureSignalRUnableToAccessException(string requestUri, Exception innerException) : base(String.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRUnableToAccessException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}