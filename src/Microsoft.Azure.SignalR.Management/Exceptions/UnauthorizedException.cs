// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class UnauthorizedException : RestApiException
    {
        public UnauthorizedException() : base("401 Unauthorized: Authentication is required and has failed or has not yet been provided.")
        {
        }

        public UnauthorizedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}