// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR.Infrastructure;

namespace Microsoft.Azure.SignalR.AspNet
{
    /// <summary>
    /// The token is no long Base64 encoded as the DefaultProtectedData does
    /// </summary>
    internal class EmptyProtectedData : IProtectedData
    {
        public string Protect(string data, string purpose)
        {
            return data;
        }

        public string Unprotect(string protectedValue, string purpose)
        {
            return protectedValue;
        }
    }
}