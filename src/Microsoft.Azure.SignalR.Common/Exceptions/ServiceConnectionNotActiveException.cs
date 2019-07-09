// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class ServiceConnectionNotActiveException : AzureSignalRException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConnectionNotActiveException"/> class.
        /// </summary>
        public ServiceConnectionNotActiveException() : base("The connection is not active, data cannot be sent to the service.")
        {
        }

        public ServiceConnectionNotActiveException(string message) : base($"The connection is not active, data cannot be sent to the service: {message}.")
        {

        }
    }
}
