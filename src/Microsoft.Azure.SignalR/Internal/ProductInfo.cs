// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.SignalR
{
    internal static class ProductInfo
    {
        public static string Get()
        {
            var packageId = typeof(ProductInfo).GetTypeInfo().Assembly.GetName().Name;
            var version = typeof(ProductInfo).GetTypeInfo().Assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            var runtime = RuntimeInformation.FrameworkDescription.Trim();
            var operatingSystem = RuntimeInformation.OSDescription.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();
            
            return $"{packageId}/{version.InformationalVersion} ({runtime}; {operatingSystem}; {processorArchitecture})";
        }
    }
}
