// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Management
{
    [Serializable]
    public class NotFoundException : RestApiException
    {
        public NotFoundException() : base("404 Not Found: The requested resource could not be found.")
        {
        }

        public NotFoundException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}