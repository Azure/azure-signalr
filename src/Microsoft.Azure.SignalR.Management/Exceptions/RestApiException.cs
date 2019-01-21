// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class RestApiException : Exception
    {
        public RestApiException(): base()
        {
        }

        public RestApiException(string message): base(message)
        {
        }

        protected RestApiException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}