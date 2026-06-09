// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Testing.xunit;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.

public class RetryFactDiscoverer(IMessageSink messageSink) : FactDiscoverer(messageSink)
{
    public override IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions,
                                                         ITestMethod testMethod,
                                                         IAttributeInfo factAttribute)
    {
        IXunitTestCase testCase;

        if (testMethod.Method.GetParameters().Any())
        {
            testCase = new ExecutionErrorTestCase(messageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod,
                "[RetryFact] methods are not allowed to have parameters. Did you mean to use [RetryTheory]?");
        }
        else if (testMethod.Method.IsGenericMethodDefinition)
        {
            testCase = new ExecutionErrorTestCase(messageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod,
                "[RetryFact] methods are not allowed to be generic.");
        }
        else
        {
            testCase = CreateTestCase(discoveryOptions, testMethod, factAttribute);
        }

        return [testCase];
    }

    protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions,
                                                     ITestMethod testMethod,
                                                     IAttributeInfo factAttribute)
    {
        var text = testMethod.EvaluateSkipConditions();
        if (text is not null)
        {
            return new SkippedTestCase(text, base.DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod);
        }
        var maxRetries =
            factAttribute.GetNamedArgument<int>(nameof(RetryFactAttribute.MaxRetries));
        var delayBetweenRetriesMs =
            factAttribute.GetNamedArgument<int>(nameof(RetryFactAttribute.DelayBetweenRetriesMs));
        var skipOnExceptions =
            factAttribute.GetNamedArgument<Type[]>(nameof(RetryFactAttribute.SkipOnExceptions));

        return new RetryTestCase(messageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod, maxRetries, delayBetweenRetriesMs,
            skipOnExceptions);
    }
}
