// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

/// <summary>
/// An XUnit message bus that can block messages from being passed until we want them to be.
/// </summary>
public sealed class BlockingMessageBus(IMessageBus underlyingMessageBus, MessageTransformer messageTransformer) : IMessageBus
{
    private ConcurrentQueue<IMessageSinkMessage> _messageQueue = new();

    public bool QueueMessage(IMessageSinkMessage rawMessage)
    {
        // Transform the message to apply any additional functionality, then intercept & store it for replay later
        var transformedMessage = messageTransformer.Transform(rawMessage);
        _messageQueue.Enqueue(transformedMessage);

        // Returns if execution should continue. Since we are intercepting the message, we
        //  have no way of checking this so always continue...
        return true;
    }

    public void Clear()
    {
        _messageQueue = new ConcurrentQueue<IMessageSinkMessage>();
    }

    /// <summary>
    /// Write the cached messages to the underlying message bus
    /// </summary>
    public void Flush()
    {
        while (_messageQueue.TryDequeue(out var message))
        {
            underlyingMessageBus.QueueMessage(message);
        }
    }

    public void Dispose()
    {
        // Do not dispose of the underlying message bus - it is an externally owned resource
    }
}
