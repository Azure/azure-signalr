// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.SignalR
{
    internal static class ProductInfo
    {
        private const int MaxLength = 128;

        public static string GetProductInfo(Assembly assembly = null)
        {
            assembly = assembly ?? Assembly.GetCallingAssembly();
            var packageId = assembly.GetName().Name;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var runtime = RuntimeInformation.FrameworkDescription?.Trim();
            var operatingSystem = RuntimeInformation.OSDescription?.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();
            var packageInfo = $"{packageId}/{version}";
            return $"{TruncateString(packageInfo)} ({runtime}; {operatingSystem}; {processorArchitecture})";
        }

        private static string TruncateString(string str, int maxLen = MaxLength)
        {
            return str.Length < maxLen ? str : $"{str.Substring(0, maxLen)}...";
        }
    }
}
