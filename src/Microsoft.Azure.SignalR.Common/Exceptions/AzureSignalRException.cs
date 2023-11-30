// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
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
    }
}
