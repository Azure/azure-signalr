// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class ServiceConnectionNotActiveException : AzureSignalRException
    {
        public string Endpoint { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConnectionNotActiveException"/> class.
        /// </summary>
        public ServiceConnectionNotActiveException() : base("The connection is not active, data cannot be sent to the service.")
        {
        }

        public ServiceConnectionNotActiveException(string endpoint) : base($"The connection is not active, data cannot be sent to the service: {endpoint}.")
        {
            Endpoint = endpoint;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConnectionNotActiveException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is <c>null</c>.</exception>
        /// <exception cref="SerializationException">The class name is <c>null</c> or <see cref="Exception.HResult"/> is zero (0).</exception>
        protected ServiceConnectionNotActiveException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Endpoint = info.GetString("Endpoint");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Endpoint", Endpoint);
            base.GetObjectData(info, context);
        }
    }
}
