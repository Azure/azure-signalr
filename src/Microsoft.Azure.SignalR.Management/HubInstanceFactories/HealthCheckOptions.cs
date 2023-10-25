// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

//used to reduce test time
internal class HealthCheckOption
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public bool EnabledForSingleEndpoint { get; set; } = false;
}