// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class OtherException : RestApiException
    {
        public OtherException(HttpStatusCode statusCode, string reasonPhrase) : base($"{statusCode} {reasonPhrase}.")
        {
        }

        public OtherException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}