// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class BadRequestException : RestApiException
    {
        public BadRequestException() : base("400 Bad Request: The server cannot or will not process the request due to an apparent client error.")
        {
        }

        public BadRequestException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}