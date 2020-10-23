// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerContext
    {
        public string ProductInfo { get; set; }

        /// <summary>
        /// A merged result got from <see cref="ServiceManagerOptions.GetMergedServiceEndpoints"/>
        /// </summary>
        public ServiceEndpoint[] ServiceEndpoints { get; set; }
    }
}
