// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a singleton service of the type specified in <typeparamref name="TI1" /> and
        /// <typeparamref name="TI2"/> with the same implementation type specified in <typeparamref name="T" /> to the
        /// specified <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" />.
        /// </summary>
        public static IServiceCollection AddSingleton<TI1, TI2, T>(this IServiceCollection services)
            where T : class, TI1, TI2
            where TI1 : class
            where TI2 : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<TI1, T>();
            services.AddSingleton<TI2, T>(x => (T)x.GetService<TI1>());
            return services;
        }

        /// <summary>
        /// Remove the service descriptor registed with implement type <typeparamref name="TImplement"/> and interface <typeparamref name="TInterface"/>
        /// </summary>
        public static IServiceCollection Remove<TInterface, TImplement>(this IServiceCollection services)
            where TImplement : class, TInterface
            where TInterface : class
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var serviceToRemove = services.Where(s => s.ServiceType == typeof(TInterface)).FirstOrDefault(s => GetImplementationType(s) == typeof(TImplement));
            if (serviceToRemove != null)
            {
                services.Remove(serviceToRemove);
            }

            return services;
        }

        private static Type GetImplementationType(ServiceDescriptor s)
        {
            if (s.ImplementationType != null)
            {
                return s.ImplementationType;
            }

            if (s.ImplementationInstance != null)
            {
                return s.ImplementationInstance.GetType();
            }

            if (s.ImplementationFactory != null)
            {
                return s.ImplementationFactory.GetType().GenericTypeArguments[1];
            }

            return null;
        }
    }
}
