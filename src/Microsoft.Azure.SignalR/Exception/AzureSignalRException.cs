// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    public class AzureSignalRException : Exception
    {
        public AzureSignalRException(string message) : base(message)
        {
        }
    }
}
