// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRException"/> class.
        /// </summary>
        public AzureSignalRException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRException"/> class.
        /// </summary>
        public AzureSignalRException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRException"/> class.
        /// </summary>
        public AzureSignalRException(string message, Exception ex) : base(message, ex)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSignalRException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is <c>null</c>.</exception>
        /// <exception cref="SerializationException">The class name is <c>null</c> or <see cref="Exception.HResult"/> is zero (0).</exception>
        #if NET8_0_OR_GREATER
        [Obsolete]
        #endif
        protected AzureSignalRException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
