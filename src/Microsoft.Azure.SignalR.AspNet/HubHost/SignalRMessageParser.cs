﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class SignalRMessageParser : IMessageParser
    {
        private const char DotChar = '.';

        private readonly HashSet<string> _hubNameWithDots = new HashSet<string>();
        private readonly HashSet<string> _hubs = new HashSet<string>();

        private readonly JsonSerializer _serializer;
        private readonly IMemoryPool _pool;
        private readonly IServiceProtocol _serviceProtocol;

        public SignalRMessageParser(IReadOnlyList<string> hubs, IDependencyResolver resolver)
        {
            _serializer = resolver.Resolve<JsonSerializer>() ?? throw new ArgumentNullException(nameof(JsonSerializer));
            _serviceProtocol = resolver.Resolve<IServiceProtocol>() ?? throw new ArgumentNullException(nameof(IServiceProtocol));
            _pool = resolver.Resolve<IMemoryPool>() ?? throw new ArgumentNullException(nameof(IMemoryPool));

            // Hubs are fetched from IHubManager.GetHubs()'s Name property, and by default it is the type Name instead of FullName, or it is the value of HubName attribute
            foreach (var hub in hubs)
            {
                // It is possible that the hub contains dot character, while the fully qualified name is formed as {HubName}.{Name} (Name can be connectionId or userId or groupId)
                // So keep a copy of the hub names containing dots and return all the possible combinations when the fully qualified name is provided
                if (hub.IndexOf(DotChar) > -1)
                {
                    _hubNameWithDots.Add(hub);
                }

                _hubs.Add(hub);
            }
        }

        public IEnumerable<AppMessage> GetMessages(Message message)
        {
            if (message.IsCommand)
            {
                var command = _serializer.Parse<Command>(message.Value, message.Encoding);
                switch (command.CommandType)
                {
                    case CommandType.AddToGroup:
                        {
                            // name is hg-{HubName}.{GroupName}, consider the whole as the actual group name
                            // this message always goes through the appName-connection
                            var groupName = command.Value;
                            var connectionId = GetName(message.Key, PrefixHelper.ConnectionIdPrefix);
                            var joinGroupWithAckMessage = new JoinGroupWithAckMessage(connectionId, groupName).WithTracingId();
                            
                            // go through the app connection
                            // use groupName as the partitionkey so that commands towards the same group always goes into the same service connection
                            yield return new AppMessage(joinGroupWithAckMessage, message);
                            yield break;
                        }
                    case CommandType.RemoveFromGroup:
                        {
                            // this message always goes through the appName-connection
                            var groupName = command.Value;
                            var connectionId = GetName(message.Key, PrefixHelper.ConnectionIdPrefix);
                            var leaveGroupWithAckMessage = new LeaveGroupWithAckMessage(connectionId, groupName).WithTracingId();
                            // go through the app connection
                            // use groupName as the partitionkey so that commands towards the same group always goes into the same service connection
                            yield return new AppMessage(leaveGroupWithAckMessage, message);
                            yield break;
                        }
                    case CommandType.Initializing:
                        yield break;
                    case CommandType.Abort:
                        yield break;
                }
            }

            var segment = GetPayload(message);

            // broadcast case
            if (TryGetName(message.Key, PrefixHelper.HubPrefix, out var hubName))
            {
                var broadcastDataMessage = new BroadcastDataMessage(excludedList: GetExcludedIds(message.Filter), payloads: GetPayloads(segment)).WithTracingId();
                yield return new HubMessage(hubName, broadcastDataMessage, message);
            }
            // echo case
            else if (TryGetName(message.Key, PrefixHelper.HubConnectionIdPrefix, out _))
            {
                // naming: hc-{HubName}.{ConnectionId}
                // ConnectionId can NEVER contain .
                var index = message.Key.LastIndexOf('.');
                if (index < 0 || index == message.Key.Length - 1)
                {
                    throw new ArgumentException($"Key must contain '.' in between but it is not: {message.Key}");
                }

                var connectionId = message.Key.Substring(index + 1);

                var connectionDataMessage = new ConnectionDataMessage(connectionId, segment).WithTracingId();

                // Go through the app connection
                yield return new AppMessage(connectionDataMessage, message);
            }
            // group broadcast case
            else if (TryGetName(message.Key, PrefixHelper.HubGroupPrefix, out _))
            {
                // naming: hg-{HubName}.{GroupName}, it as a whole is the group name per the JoinGroup implementation
                // go through the app connection
                // use groupName as the partitionkey so that commands towards the same group always goes into the same service connection
                var groupName = message.Key;
                var groupBroadcastDataMessage = new GroupBroadcastDataMessage(groupName, excludedList: GetExcludedIds(message.Filter), payloads: GetPayloads(segment)).WithTracingId();
                yield return new AppMessage(groupBroadcastDataMessage, message);
            }
            // user case
            else if (TryGetName(message.Key, PrefixHelper.HubUserPrefix, out var userWithHubPrefix))
            {
                // naming: hu-{HubName}.{UserName}, HubName can contain '.' and UserName can contain '.'
                // Go through all the possibilities
                foreach (var (hub, user) in GetPossibleNames(userWithHubPrefix))
                {
                    var userDataMessage = new UserDataMessage(user, GetPayloads(segment)).WithTracingId();
                    // For old protocol, it is always single user per message https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Infrastructure/Connection.cs#L162
                    yield return new HubMessage(hub, userDataMessage, message);
                }
            }
            else
            {
                throw new NotSupportedException($"Message {message.Key} is not supported.");
            }
        }

        private ReadOnlyMemory<byte> GetPayload(Message message)
        {
            IJsonWritable value = new PersistentResponse(m => false, tw => tw.Write("0"))
            {
                Messages = new List<ArraySegment<Message>>
                {
                    new ArraySegment<Message>(new[] {message})
                },
                TotalCount = 1
            };

            using (var writer = new MemoryPoolTextWriter(_pool))
            {
                value.WriteJson(writer);
                writer.Flush();

                // Reuse ConnectionDataMessage to wrap the payload
                return _serviceProtocol.GetMessageBytes(new ConnectionDataMessage(string.Empty, writer.Buffer));
            }
        }

        private IEnumerable<(string hub, string name)> GetPossibleNames(string fullName)
        {
            var index = fullName.IndexOf(DotChar);
            if (index == -1)
            {
                throw new InvalidDataException($"Name {fullName} does not contain the required separator {DotChar}");
            }

            // It is rare that hubname contains '.'
            foreach (var hub in _hubNameWithDots)
            {
                if (fullName.Length > hub.Length + 1
                    && fullName[hub.Length] == DotChar
                    && hub == fullName.Substring(0, hub.Length))
                {
                    yield return (hub, fullName.Substring(hub.Length + 1));
                }
            }

            var hubName = fullName.Substring(0, index);
            if (_hubs.Contains(hubName))
            {
                var name = fullName.Substring(index + 1);
                yield return (hubName, name);
            }
        }

        private static IReadOnlyList<string> GetExcludedIds(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return null;
            }

            return filter.Split('|').Select(s => GetName(s, PrefixHelper.ConnectionIdPrefix)).ToArray();
        }

        private static Dictionary<string, ReadOnlyMemory<byte>> GetPayloads(ReadOnlyMemory<byte> data)
        {
            return new Dictionary<string, ReadOnlyMemory<byte>>
            {
                { "json", data }
            };
        }

        private static bool TryGetName(string qualifiedName, string prefix, out string name)
        {
            if (qualifiedName.Length > prefix.Length && qualifiedName.StartsWith(prefix))
            {
                name = qualifiedName.Substring(prefix.Length);

                return true;
            }

            name = default;
            return false;
        }

        private static string GetName(string qualifiedName, string prefix)
        {
            if (TryGetName(qualifiedName, prefix, out var name))
            {
                return name;
            }

            throw new InvalidDataException($"{qualifiedName} is not valid.");
        }
    }
}
