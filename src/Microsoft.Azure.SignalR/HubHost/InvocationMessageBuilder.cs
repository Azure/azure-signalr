// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class InvocationMessageBuilder
    {
        private readonly InvocationMessage _message;

        public InvocationMessageBuilder(string invocationId, string method, object[] args)
        {
            _message = new InvocationMessage(invocationId, method, null, args);
        }

        public InvocationMessage Build()
        {
            return _message;
        }

        public InvocationMessageBuilder WithAction(string action)
        {
            _message.AddAction(action);
            return this;
        }

        public InvocationMessageBuilder WithConnectionId(string connectionId)
        {
            _message.AddConnectionId(connectionId);
            return this;
        }

        public InvocationMessageBuilder WithConnectionIds(IReadOnlyList<string> connectionIds)
        {
            _message.AddConnectionIds(connectionIds);
            return this;
        }

        public InvocationMessageBuilder WithUser(string userId)
        {
            _message.AddUser(userId);
            return this;
        }

        public InvocationMessageBuilder WithUsers(IReadOnlyList<string> userIds)
        {
            _message.AddUsers(userIds);
            return this;
        }

        public InvocationMessageBuilder WithGroup(string group)
        {
            _message.AddGroupName(group);
            return this;
        }

        public InvocationMessageBuilder WithGroups(IReadOnlyList<string> groups)
        {
            _message.AddGroupNames(groups);
            return this;
        }

        public InvocationMessageBuilder WithExcludedIds(IReadOnlyList<string> excludedIds)
        {
            _message.AddExcludedIds(excludedIds);
            return this;
        }
    }
}
