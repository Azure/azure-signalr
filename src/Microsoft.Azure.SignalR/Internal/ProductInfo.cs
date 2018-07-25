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
        public static string GetProductInfo()
        {
            var assembly = typeof(ProductInfo).GetTypeInfo().Assembly;
            var packageId = assembly.GetName().Name;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var runtime = RuntimeInformation.FrameworkDescription.Trim();
            var operatingSystem = RuntimeInformation.OSDescription.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();
            
            return $"{packageId}/{version} ({runtime}; {operatingSystem}; {processorArchitecture})";
        }
    }
}
