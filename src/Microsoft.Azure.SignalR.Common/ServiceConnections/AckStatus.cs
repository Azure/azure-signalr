// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Azure.SignalR
{
    internal static class AckStatus
    {
        public const int Ok = 1;
        public const int NotFound = 2;
        public const int Timeout = 3;
    }
}
