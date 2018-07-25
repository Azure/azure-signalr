// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ProductInfoFacts
    {
        [Fact]
        public void ProductInfo()
        {
            var productInfo = new ProductInfo();
            Assert.NotNull(productInfo.ToString());

            var packageId = typeof(ProductInfo).GetTypeInfo().Assembly.GetName().Name;
            var version = typeof(ProductInfo).GetTypeInfo().Assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            var runtime = RuntimeInformation.FrameworkDescription.Trim();
            var operatingSystem = RuntimeInformation.OSDescription.Trim();
            var processorArchitecture = RuntimeInformation.ProcessArchitecture.ToString().Trim();

            var userAgent = $"{packageId}/{version.InformationalVersion} ({runtime}; {operatingSystem}; {processorArchitecture})";

            Assert.Equal(productInfo.ToString(), userAgent);
        }
    }
}
