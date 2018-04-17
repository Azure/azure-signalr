// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR;

namespace Owin
{
    public static class AzureSignalRAppBuilderExtensions
    {
        public static IAppBuilder UseAzureSignalR(this IAppBuilder app, Action<HubHostBuilder> configure)
        {
            // TODO.
            //var builder = new HubHostBuilder(CloudSignalR.ServiceProvider);
            //configure(builder);

            return app;
        }
    }
}
