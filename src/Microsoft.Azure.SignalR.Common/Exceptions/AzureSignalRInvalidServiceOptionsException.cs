// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRInvalidServiceOptionsException : AzureSignalRException
    {
        public AzureSignalRInvalidServiceOptionsException(string propertyName, string validScope) 
            : base($"Property '{propertyName}' value should be {validScope}.")
        {
        }
    }
}
