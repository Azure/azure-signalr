// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using System;

namespace Owin
{
    public static partial class OwinExtensions
    {
        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder)
        {
            return builder.MapAzureSignalR(new HubConfiguration());
        }

        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, HubConfiguration configuration)
        {
            return builder.MapAzureSignalR("/signalr", configuration);
        }

        public static IAppBuilder MapAzureSignalR(this IAppBuilder builder, string path, HubConfiguration configuration)
        {
            return builder.Map(path, subApp => subApp.RunAzureSignalR(configuration));
        }

        public static void RunAzureSignalR(this IAppBuilder builder)
        {
            builder.RunAzureSignalR(new HubConfiguration());
        }

        public static void RunAzureSignalR(this IAppBuilder builder, HubConfiguration configuration)
        {
            throw new NotImplementedException();
        }
    }
}
