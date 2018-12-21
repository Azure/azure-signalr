// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Common
{
    public class AccessTokenTooLongException : Exception
    {
        public AccessTokenTooLongException() : base($"AccessToken must not be longer than 4K.")
        {
        }
    }
}
