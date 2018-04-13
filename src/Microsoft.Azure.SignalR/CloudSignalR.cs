// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public class CloudSignalR
    {
        public static SignalRServiceContext<THub> CreateServiceContext<THub>() where THub : Hub
        {
            var hubMessageSender = ServiceProvider.GetRequiredService<IHubMessageSender>();
            var connectionServiceProvider = ServiceProvider.GetRequiredService<IConnectionServiceProvider>();

            var signalrServiceHubContext = new SignalRServiceHubContext<THub>(connectionServiceProvider, hubMessageSender);
            return new SignalRServiceContext<THub>(connectionServiceProvider, signalrServiceHubContext);
        }

        public static void ConfigureAuthorization(Action<AuthorizationOptions> configure)
        {
            if (configure != null) _authorizationConfigure = configure;
        }

        private static Action<AuthorizationOptions> _authorizationConfigure = _ => { };

        private static IServiceProvider _externalServiceProvider = null;

        private static IServiceCollection _externalServiceCollection = null;

        private static readonly Lazy<IServiceProvider> _internalServiceProvider =
            new Lazy<IServiceProvider>(
                () =>
                {
                    var serviceCollection = new ServiceCollection();
                    var signalRServerBuilder = serviceCollection.AddSignalR().AddAzureSignalR();
                    signalRServerBuilder.Services.AddLogging();
                    signalRServerBuilder.Services.AddAuthorization(_authorizationConfigure);
                    return signalRServerBuilder.Services.BuildServiceProvider();
                });

        public static IServiceProvider ServiceProvider
        {
            get => _externalServiceProvider ?? (_externalServiceCollection != null ? _externalServiceCollection.BuildServiceProvider() : _internalServiceProvider.Value);
            internal set => _externalServiceProvider = value;
        }

        public static IServiceCollection ServiceCollection
        {
            get => _externalServiceCollection;
            internal set => _externalServiceCollection = value;
        }
    }
}
