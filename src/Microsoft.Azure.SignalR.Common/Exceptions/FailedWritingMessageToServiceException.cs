// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common
{
    public class FailedWritingMessageToServiceException : ServiceConnectionNotActiveException
    {
        public string EndpointUri { get; }

        public FailedWritingMessageToServiceException(string endpointUri) : base($"Unable to write message to endpoint: {endpointUri}")
        {
            EndpointUri = endpointUri;
        }
    }
}
