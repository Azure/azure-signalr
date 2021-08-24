// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    //used to reduce test time
    internal class HealthCheckOption
    {
        public TimeSpan CheckInterval { get; set; }
        public TimeSpan RetryInterval { get; set; }
    }
}