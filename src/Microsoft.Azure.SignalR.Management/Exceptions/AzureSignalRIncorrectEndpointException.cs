// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class AzureSignalRIncorrectEndpointException : AzureSignalRException
    {
        private const string _message = "Endpoint incorrect or DNS error.";

        public AzureSignalRIncorrectEndpointException(string requestUri, Exception innerException) : base(String.IsNullOrEmpty(requestUri) ? _message : $"{_message} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRIncorrectEndpointException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}