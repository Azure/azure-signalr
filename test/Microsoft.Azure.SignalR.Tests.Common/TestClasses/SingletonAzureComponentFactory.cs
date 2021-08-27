// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public static class SingletonAzureComponentFactory
    {
        public static readonly AzureComponentFactory Instance;
        
        static SingletonAzureComponentFactory()
        {
            var services = new ServiceCollection();
            services.AddAzureClientsCore();
            Instance = services.BuildServiceProvider().GetRequiredService<AzureComponentFactory>();
        }
    }
}
