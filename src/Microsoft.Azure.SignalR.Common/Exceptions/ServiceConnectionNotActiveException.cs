// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class ServiceConnectionNotActiveException : AzureSignalRException
    {
        private const string NotActiveMessage = "The connection is not active, data cannot be sent to the service.";
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConnectionNotActiveException"/> class.
        /// </summary>
        public ServiceConnectionNotActiveException() : this(NotActiveMessage)
        {
        }

        public ServiceConnectionNotActiveException(string message) : base(string.IsNullOrEmpty(message) ? NotActiveMessage : message)
        {

        }

#if NET8_0_OR_GREATER
        [Obsolete]
#endif
        protected ServiceConnectionNotActiveException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
