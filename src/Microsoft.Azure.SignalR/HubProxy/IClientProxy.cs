// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public interface IClientProxy
    {
        /// <summary>
        /// Invokes a method on the connection(s) in Azure SignalR represented by the <see cref="IClientProxy"/> instance.
        /// </summary>
        /// <param name="method">name of the method to invoke</param>
        /// <param name="args">arguments to pass to the client</param>
        /// <returns>A task that represents when the data has been sent to the client in SignalR service.</returns>
        Task<HttpResponseMessage> SendAsync(string method, object[] args);
    }
}
