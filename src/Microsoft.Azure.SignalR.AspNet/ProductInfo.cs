// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal static class ProductInfo
    {
        /// <summary>
        /// For .NET framework below netframework462, there are assembly binding issues when referencing netstandard assemblies, https://github.com/Azure/azure-signalr/issues/452
        /// For now, disable usage of System.Runtime.InteropServices.RuntimeInformation
        /// </summary>
        /// <returns></returns>
        public static string GetProductInfo()
        {
            var assembly = Assembly.GetCallingAssembly();
            var packageId = assembly.GetName().Name;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            
            return $"{packageId}/{version}";
        }
    }
}
