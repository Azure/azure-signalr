// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal static class HubRouteBuilderExtension
    {
        private static readonly MethodInfo _mapHub = typeof(HubRouteBuilder).GetMethod("MapHub", new[] { typeof(PathString) });

        public static void MapHub(HubRouteBuilder route, Type hubType, PathString path)
        {
            _mapHub.MakeGenericMethod(hubType).Invoke(route, new object[] { path });
        }
    }
}
