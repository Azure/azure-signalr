// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRInvalidEndpointException
        : AzureSignalRException
    {
        private static readonly string Key = typeof(ServiceEndpoint).FullName;

        public AzureSignalRInvalidEndpointException(ServiceEndpoint[] invalidEndpoints) : base("Sepecifed endpoints are invalid. Make sure they are configured.")
        {
            Data[Key] = invalidEndpoints;
        }

        protected AzureSignalRInvalidEndpointException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}