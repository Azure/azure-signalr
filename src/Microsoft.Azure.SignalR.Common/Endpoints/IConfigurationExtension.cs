// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR
{
    internal static class IConfigurationExtension
    {
        public static ServiceEndpoint[] GetSignalRServiceEndpoints(this IConfiguration configuration, string sectionKey)
        {
            var section = configuration.GetSection(sectionKey);
            return GetEndpoints(section).ToArray();
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