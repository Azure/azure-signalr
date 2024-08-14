// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class DynamicHubContextStore : IDynamicHubContextStore
    {
        private readonly ModuleBuilder _hubModule =
            AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("temp"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("TempModule");

        private readonly ConcurrentDictionary<string, Lazy<DynamicHubContext>> _hubContextCache = new ConcurrentDictionary<string, Lazy<DynamicHubContext>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _hubNamespace = "TempNamespace";
        private readonly IServiceProvider _provider;

        private readonly object _lock = new object();

        public DynamicHubContextStore(IServiceProvider provider)
        {
            _provider = provider;
        }

        public bool TryGetLifetimeContext(string hub, out DynamicHubContext context)
        {
            if (_hubContextCache.TryGetValue(hub, out var c))
            {
                context = c.Value;
                return true;
            }

            context = null;
            return false;
        }

        public DynamicHubContext GetOrAdd(string hub)
        {
            return _hubContextCache.GetOrAdd(hub, s => new Lazy<DynamicHubContext>(() => CreateHubContextImpl(hub), true)).Value;
        }

        private DynamicHubContext CreateHubContextImpl(string hub)
        {
            TypeBuilder htb;
            lock (_lock)
            {
                htb = _hubModule.DefineType($"{_hubNamespace}.{hub}",
                        TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(Hub));
            }

            htb.DefineDefaultConstructor(MethodAttributes.Public);
            var hubType = htb.CreateType();
            var context = typeof(IHubContext<>).MakeGenericType(hubType);
            var hubContext = _provider.GetService(context);
            var clients = (IHubClients)context.GetProperty("Clients").GetValue(hubContext);
            var handlerType = typeof(HubProxyHandler<>).MakeGenericType(hubType);

            var connectionHandler = (ConnectionHandler)_provider.GetService(handlerType);
            var lifetimeType = typeof(HubLifetimeManager<>).MakeGenericType(hubType);
            var lifetime = _provider.GetService(lifetimeType) as IHubLifetimeManager;
            return new DynamicHubContext(hubType, clients, lifetime, connectionHandler);
        }
    }
}
