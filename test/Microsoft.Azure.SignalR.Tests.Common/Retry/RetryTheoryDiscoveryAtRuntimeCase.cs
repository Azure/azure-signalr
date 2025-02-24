// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

/// <summary>
/// Represents a test case to be retried which runs multiple tests for theory data, either because the
/// data was not enumerable or because the data was not serializable.
/// Equivalent to xunit's XunitTheoryTestCase
/// </summary>
[Serializable]
public class RetryTheoryDiscoveryAtRuntimeCase : XunitTestCase, IRetryableTestCase
{
    public int MaxRetries { get; private set; }

    public int DelayBetweenRetriesMs { get; private set; }

    public string[] SkipOnExceptionFullNames { get; private set; } = [];

    /// <summary/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public RetryTheoryDiscoveryAtRuntimeCase()
    { }

    public RetryTheoryDiscoveryAtRuntimeCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        ITestMethod testMethod,
        int maxRetries,
        int delayBetweenRetriesMs,
        Type[] skipOnExceptions)
        : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
    {
        MaxRetries = maxRetries;
        DelayBetweenRetriesMs = delayBetweenRetriesMs;
        SkipOnExceptionFullNames = RetryTestCase.GetSkipOnExceptionFullNames(skipOnExceptions);
    }

    /// <inheritdoc />
    public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus,
        object[] constructorArguments, ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        return RetryTestCaseRunner.RunAsync(this, diagnosticMessageSink, messageBus, cancellationTokenSource,
            blockingMessageBus => new XunitTheoryTestCaseRunner(this, DisplayName, SkipReason, constructorArguments,
                    diagnosticMessageSink, blockingMessageBus, aggregator, cancellationTokenSource)
                .RunAsync());
    }

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);

        data.AddValue("MaxRetries", MaxRetries);
        data.AddValue("DelayBetweenRetriesMs", DelayBetweenRetriesMs);
        data.AddValue("SkipOnExceptionFullNames", SkipOnExceptionFullNames);
    }

    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);

        MaxRetries = data.GetValue<int>("MaxRetries");
        DelayBetweenRetriesMs = data.GetValue<int>("DelayBetweenRetriesMs");
        SkipOnExceptionFullNames = data.GetValue<string[]>("SkipOnExceptionFullNames");
    }
}
