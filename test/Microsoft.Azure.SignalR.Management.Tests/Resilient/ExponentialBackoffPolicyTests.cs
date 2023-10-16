// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.Resilient;

public class ExponentialBackoffPolicyTests
{
    [Fact]
    public void GetDelays_ReturnsExpectedDelays()
    {
        // Arrange
        var options = Options.Create(new ServiceManagerOptions
        {
            RetryOptions = new RetryOptions
            {
                Mode = RetryMode.Exponential,
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10)
            }
        });
        var provider = new ExponentialBackOffPolicy(options);

        // Act
        var delays = provider.GetDelays();

        // Assert
        var expectedDelays = new List<TimeSpan>
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(10)
        };
        Assert.Equal(expectedDelays, delays);
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenRetryModeIsNotExponential()
    {
        // Arrange
        var options = Options.Create(new ServiceManagerOptions
        {
            RetryOptions = new RetryOptions
            {
                Mode = RetryMode.Fixed,
                MaxRetries = 5,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10)
            }
        });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ExponentialBackOffPolicy(options));
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenRetryOptionsIsNull()
    {
        // Arrange
        var options = Options.Create(new ServiceManagerOptions());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ExponentialBackOffPolicy(options));
    }
}
