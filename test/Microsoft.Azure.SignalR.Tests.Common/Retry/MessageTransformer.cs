// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

public class MessageTransformer(string[] skipOnExceptionFullNames)
{
    public bool Skipped { get; private set; }

    /// <summary>
    /// Transforms a message received from an xUnit test into another message, replacing it
    /// where necessary to add additional functionality, e.g. dynamic skipping
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public IMessageSinkMessage Transform(IMessageSinkMessage message)
    {
        // If this is a message saying that the test has been skipped, replace the message with skipping the test
        if (message is TestFailed failed && failed.ExceptionTypes.ContainsAny(skipOnExceptionFullNames))
        {
            var reason = failed.Messages?.FirstOrDefault() ?? string.Empty;
            Skipped = true;
            return new TestSkipped(failed.Test, reason);
        }

        // Otherwise this isn't a message saying the test is skipped, follow usual intercept for replay later behaviour
        return message;
    }
}
