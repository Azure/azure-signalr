// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    internal class PayloadMessage
    {
        public string Target { get; set; }

        public object[] Arguments { get; set; }
    }
}