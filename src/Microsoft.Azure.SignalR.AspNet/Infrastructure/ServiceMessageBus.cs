// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Messaging;

namespace Microsoft.Azure.SignalR.AspNet
{
    /// <summary>
    /// TODO: This class is responsible for transform SignalR Messages to ServiceMessages and send them to the service runtime
    /// </summary>
    internal class ServiceMessageBus : MessageBus
    {
        public ServiceMessageBus(IDependencyResolver resolver) : base(resolver)
        {
        }

        public override Task Publish(Message message)
        {
            throw new NotImplementedException();
        }
    }
}
