// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

public class ServiceManagerRetryOptions
{
    /// <summary>
    /// The maximum number of retry attempts before giving up.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// The delay between retry attempts for a fixed approach or the delay
    /// on which to base calculations for a backoff-based approach.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(0.8);

    /// <summary>
    /// The maximum permissible delay between retry attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The approach to use for calculating retry delays.
    /// </summary>
    public ServiceManagerRetryMode Mode { get; set; } = ServiceManagerRetryMode.Fixed;
}


