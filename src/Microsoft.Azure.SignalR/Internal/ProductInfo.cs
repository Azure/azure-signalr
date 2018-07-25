// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.SignalR
{
    internal class ProductInfo
    {
        public override string ToString()
        {
            var packageId = Assembly.GetExecutingAssembly().GetName().Name;
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            var runtime = RuntimeInformation.FrameworkDescription.Trim();
            var operatingSystem = RuntimeInformation.OSDescription.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();
            
            return $"{packageId}/{version.InformationalVersion} ({runtime}; {operatingSystem}; {processorArchitecture})";
        }

        public static Dictionary<string, string> ToHeader()
        {
            var productInfo = new ProductInfo();
            return new Dictionary<string, string> { { "User-Agent", productInfo.ToString() } };
        }
    }
}
