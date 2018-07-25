// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.SignalR
{
    internal class ProductInfo
    {
        public string Extra { get; set; } = "";

        public override string ToString()
        {
            var packageId = Assembly.GetExecutingAssembly().GetName().Name;
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            var runtime = RuntimeInformation.FrameworkDescription.Trim();
            var operatingSystem = RuntimeInformation.OSDescription.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();

            var userAgent = $"{packageId}/{version.InformationalVersion} ({runtime}; {operatingSystem}; {processorArchitecture})";

            if (!String.IsNullOrWhiteSpace(Extra))
            {
                userAgent += $" {Extra.Trim()}";
            }

            return userAgent;
        }
    }
}
