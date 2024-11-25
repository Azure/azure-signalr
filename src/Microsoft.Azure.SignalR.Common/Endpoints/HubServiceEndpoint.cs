// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal class HubServiceEndpoint : ServiceEndpoint
{
    private static long s_currentIndex;

    private readonly ServiceEndpoint _endpoint;

    private readonly long _uniqueIndex;

    private TaskCompletionSource<bool> _scaleTcs;

    public string Hub { get; }

    public override string Name => _endpoint.Name;

    public IServiceEndpointProvider Provider { get; }

    public IServiceConnectionContainer ConnectionContainer { get; set; }

    /// <summary>
    /// Task waiting for HubServiceEndpoint turn ready when live add/remove endpoint
    /// </summary>
    public Task ScaleTask => _scaleTcs?.Task ?? Task.CompletedTask;

    public long UniqueIndex => _uniqueIndex;

    // Value here is not accurate.
    internal override bool PendingReload => throw new NotSupportedException();

    public HubServiceEndpoint(string hub,
                              IServiceEndpointProvider provider,
                              ServiceEndpoint endpoint) : base(endpoint)
    {
        Hub = hub;
        Provider = provider;
        _endpoint = endpoint;
        _scaleTcs = endpoint.PendingReload ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        _uniqueIndex = Interlocked.Increment(ref s_currentIndex);
    }

    public void CompleteScale()
    {
        _scaleTcs?.TrySetResult(true);
    }

    // When remove an existing HubServiceEndpoint.
    public void ResetScale()
    {
        _scaleTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public override string ToString()
    {
        return base.ToString() + $"(hub={Hub})";
    }
}
