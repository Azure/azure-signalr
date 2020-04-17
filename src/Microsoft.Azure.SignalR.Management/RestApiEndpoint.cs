// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    public class RestApiEndpoint
    {
        public string Audience { get; }
        public string Token { get; }

        public RestApiEndpoint(string endpoint, string token)
        {
            Audience = endpoint;
            Token = token;
        }
    }
}
