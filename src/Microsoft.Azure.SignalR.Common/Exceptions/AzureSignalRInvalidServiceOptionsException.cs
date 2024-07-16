// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRInvalidServiceOptionsException : AzureSignalRException
    {
        public AzureSignalRInvalidServiceOptionsException(string propertyName, string validScope) 
            : base($"Property '{propertyName}' value should be {validScope}.")
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete]
#endif
        protected AzureSignalRInvalidServiceOptionsException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
