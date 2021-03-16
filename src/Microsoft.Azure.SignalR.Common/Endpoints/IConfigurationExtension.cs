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
        public static IEnumerable<ServiceEndpoint> GetEndpoints(this IConfiguration configuration, string sectionName)
        {
            var section = configuration.GetSection(sectionName);
            return section.AsEnumerable(true)
                          .Where(entry => !string.IsNullOrEmpty(entry.Value))
                          .Select(entry => new ServiceEndpoint(entry.Key, entry.Value));
        }
    }
}