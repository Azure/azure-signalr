// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR
{
    internal static class IConfigurationExtension
    {
        public static IEnumerable<ServiceEndpoint> GetEndpoints(this IConfiguration configuration, string sectionName)
        {
            var section = configuration.GetSection(sectionName);
            return section.AsEnumerable(true)
                          .Where(entry => !string.IsNullOrEmpty(entry.Value))
                          .Select(entry => new ServiceEndpoint(entry.Key, entry.Value));
        }

        public static IEnumerable<ServiceEndpoint> GetEndpoints(this IConfiguration config, AzureComponentFactory azureComponentFactory)
        {
            foreach (var child in config.GetChildren())
            {
                var serviceEndpoint = child.GetNamedEndpointFromIdentity(azureComponentFactory);
                if (serviceEndpoint != null)
                {
                    yield return serviceEndpoint;
                }
                else
                {
                    foreach(var endpoint in GetNamedEndpointsFromConnectionString(child))
                    {
                        yield return endpoint;
                    }
                }
            }
        }

        public static IEnumerable<ServiceEndpoint> GetNamedEndpointsFromConnectionString(this IConfigurationSection section)
        {
            var endpointName = section.Key;
            if (section.Value != null)
            {
                yield return new ServiceEndpoint(section.Key, section.Value);
            }
            //This part isn't responsible for deduplicating.
            if (section["primary"] != null)
            {
                yield return new ServiceEndpoint(section["primary"], EndpointType.Primary, endpointName);
            }
            if (section["secondary"] != null) {
                yield return new ServiceEndpoint(section["secondary"], EndpointType.Secondary, endpointName);
            }
        }

        public static ServiceEndpoint GetNamedEndpointFromIdentity(this IConfigurationSection section, AzureComponentFactory azureComponentFactory)
        {
            var uri = section[Constants.Keys.ServiceUriKey];
            if (uri != null)
            {
                var name = section.Key;
                var type = section.GetValue(Constants.Keys.EndpointTypeKey, EndpointType.Primary);
                var credential = azureComponentFactory.CreateTokenCredential(section);
                return new ServiceEndpoint(new Uri(uri), credential, type, name);
            }
            return null;
        }
    }
}