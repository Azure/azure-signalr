// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR.Common.Endpoints
{
    public static class IConfigurationExtension
    {
        public static (string ConnectionString, ServiceEndpoint[]) GetSignalRServiceEndpoints(this IConfiguration configuration, string sectionKey)
        {
            var section = configuration.GetSection(sectionKey);
            var connectionString = section.Value;
            var endpoints = GetEndpoints(section).ToArray();

            return (connectionString, endpoints);
        }

        private static IEnumerable<ServiceEndpoint> GetEndpoints(IConfiguration section)
        {
            foreach (var entry in section.AsEnumerable(true))
            {
                if (!string.IsNullOrEmpty(entry.Value))
                {
                    yield return new ServiceEndpoint(entry.Key, entry.Value);
                }
            }
        }
    }
}