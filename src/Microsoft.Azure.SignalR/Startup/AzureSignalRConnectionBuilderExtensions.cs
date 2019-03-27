// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Reflection;

namespace Microsoft.Azure.SignalR.Startup
{
    public static class AzureSignalRConnectionBuilderExtensions
    {
        private static readonly MethodInfo _useHubMethod = typeof(SignalRConnectionBuilderExtensions).GetMethod(nameof(SignalRConnectionBuilderExtensions.UseHub));

        // A late bound version of UseHub<T>
        public static IConnectionBuilder UseHub(this IConnectionBuilder builder, Type hubType)
        {
            return (IConnectionBuilder)_useHubMethod.MakeGenericMethod(hubType).Invoke(null, new object[] { builder });
        }
    }
}
