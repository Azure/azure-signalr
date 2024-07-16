// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRRuntimeException : AzureSignalRException
    {
        private const string ErrorMessage = "Azure SignalR service runtime error.";

        public AzureSignalRRuntimeException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete]
#endif
        protected AzureSignalRRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}