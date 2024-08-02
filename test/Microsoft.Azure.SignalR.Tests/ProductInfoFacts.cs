// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ProductInfoFacts
{
    [Fact]
    public void GetProductInfo()
    {
        var productInfo = ProductInfo.GetProductInfo();

        Assert.NotNull(productInfo);
    }
}
