// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class FailedWritingMessageToServiceException : ServiceConnectionNotActiveException
    {
        public string EndpointUri { get; }

        public FailedWritingMessageToServiceException(string endpointUri) : base($"Unable to write message to endpoint: {endpointUri}")
        {
            EndpointUri = endpointUri;
        }

#if NET8_0_OR_GREATER
        [Obsolete]
#endif
        protected FailedWritingMessageToServiceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("EndpointUri", EndpointUri);
            base.GetObjectData(info, context);
        }
    }
}
