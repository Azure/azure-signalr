// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

/// <summary>
/// The type of approach to apply when calculating the delay between retry attempts.
/// </summary>
public enum ServiceManagerRetryMode
{
    /// <summary>
    /// Retry attempts happen at fixed intervals; each delay is a consistent duration.
    /// </summary>
    Fixed,
    /// <summary>
    /// Retry attempts will delay based on a backoff strategy, where each attempt will
    ///     increase the duration that it waits before retrying.
    /// </summary>
    Exponential
}