// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR
{
    internal static class ProductInfo
    {
        public static string GetProductInfo()
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblies = (from assem in allAssemblies
                              select (Assem: assem, Attr: (ProductInfoAttribute)Attribute.GetCustomAttribute(assem, typeof(ProductInfoAttribute)))
                              into assemWithAttr
                              where assemWithAttr.Attr != null
                              orderby assemWithAttr.Attr.Priority descending
                              select assemWithAttr).ToList();

            if (assemblies == null || assemblies.Count == 0)
            {
                return "";
            };

            var maxPriority = assemblies.First().Attr.Priority;

            var productInfos = from assem in assemblies
                               where assem.Attr.Priority == maxPriority
                               select GetProductInfoCore(assem.Assem);

            return string.Join("; ", productInfos).Substring(0, 256);
        }

        private static string GetProductInfoCore(Assembly assembly)
        {
            var packageId = assembly.GetName().Name;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var runtime = RuntimeInformation.FrameworkDescription?.Trim();
            var operatingSystem = RuntimeInformation.OSDescription?.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();
            return $"{packageId}/{version} ({runtime}; {operatingSystem}; {processorArchitecture})";
        }
    }
}
