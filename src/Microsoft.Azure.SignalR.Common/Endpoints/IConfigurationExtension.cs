// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR
{
    internal static class IConfigurationExtension
    {
        /// <param name="configuration"></param>
        /// <param name="sectionName"></param>
        /// <param name="includeNoSuffixEndpoint">Include the service endpoint whose key has no suffix. </param>
        public static IEnumerable<ServiceEndpoint> GetEndpoints(this IConfiguration configuration, string sectionName, bool includeNoSuffixEndpoint = false)
        {
            var section = configuration.GetSection(sectionName);
            var suffixedEndpoints = section.AsEnumerable(true)
                                           .Where(entry => !string.IsNullOrEmpty(entry.Value))
                                           .Select(entry => new ServiceEndpoint(entry.Key, entry.Value));
            return includeNoSuffixEndpoint ? ServiceEndpointUtility.Merge(configuration[sectionName], suffixedEndpoints) : suffixedEndpoints;
        }
    }
}