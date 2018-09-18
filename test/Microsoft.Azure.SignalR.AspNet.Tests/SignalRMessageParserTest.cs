// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class SignalRMessageParserTest
    {
        private readonly IDependencyResolver _resolver = GetDefaultResolver();
        private readonly MemoryPool _pool = new MemoryPool();
        private readonly JsonSerializer _serializer = new JsonSerializer();

        [Theory]
        [InlineData("")]
        [InlineData("key")]
        [InlineData("pc-SignalRMessageParserTest")]
        [InlineData("pcg-GroupSignalRMessageParserTest")]
        [InlineData("c-GroupSignalRMessageParserTest")]
        public void TestGetMessageWithUnknownKeyThrows(string key)
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var raw = "<script type=\"\"></script>";
            var message = new Message("foo", key, new ArraySegment<byte>(Encoding.Default.GetBytes(raw)));

            var result = Assert.Throws<NotSupportedException>(() => parser.GetMessages(message).ToList());
        }

        [Fact]
        public void TestAddToGroupCommandMessage()
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var groupName = GenerateRandomName();
            var command = new Command
            {
                CommandType = CommandType.AddToGroup,
                Value = groupName,
                WaitForAck = true
            };

            var connectionId = GenerateRandomName();
            var message = CreateMessage(PrefixHelper.GetConnectionId(connectionId), command);

            var msgs = parser.GetMessages(message).ToList();
            Assert.Single(msgs);
            var msg = msgs[0].Message as JoinGroupMessage;
            Assert.NotNull(msg);
            Assert.Equal(connectionId, msg.ConnectionId);
            Assert.Equal(groupName, msg.GroupName);
        }

        [Fact]
        public void TestRemoveFromGroupCommandMessage()
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var groupName = GenerateRandomName();
            var command = new Command
            {
                CommandType = CommandType.RemoveFromGroup,
                Value = groupName,
                WaitForAck = true
            };

            var connectionId = GenerateRandomName();
            var message = CreateMessage(PrefixHelper.GetConnectionId(connectionId), command);

            var msgs = parser.GetMessages(message).ToList();
            Assert.Single(msgs);
            var msg = msgs[0].Message as LeaveGroupMessage;
            Assert.NotNull(msg);
            Assert.Equal(connectionId, msg.ConnectionId);
            Assert.Equal(groupName, msg.GroupName);
        }

        [Theory]
        [InlineData(CommandType.Initializing)]
        [InlineData(CommandType.Abort)]
        public void TestOtherGroupCommandMessagesAreIgnored(CommandType type)
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var groupName = GenerateRandomName();
            var command = new Command
            {
                CommandType = type,
                Value = groupName,
                WaitForAck = true
            };

            var connectionId = GenerateRandomName();
            var message = CreateMessage(PrefixHelper.GetConnectionId(connectionId), command);

            var msgs = parser.GetMessages(message).ToList();
            Assert.Empty(msgs);
        }

        [Theory]
        [InlineData("msg")]
        [InlineData(null)]
        [InlineData("")]
        public void TestHubMessage(string input)
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var groupName = GenerateRandomName();

            var connectionId = GenerateRandomName();
            var message = CreateMessage(PrefixHelper.GetHubName(connectionId), input);
            var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
            message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

            var msgs = parser.GetMessages(message).ToList();
            Assert.Single(msgs);
            var msg = msgs[0].Message as BroadcastDataMessage;
            Assert.NotNull(msg);
            Assert.Equal<string>(excludedConnectionIds, msg.ExcludedList);
            Assert.Equal(input, msg.Payloads["json"].GetSingleFramePayload());
        }

        [Theory]
        [InlineData("msg", "hub1")]
        [InlineData(null, "hub.hub1")]
        [InlineData("", "hub.hub1.h.hub2")]
        public void TestHubConnectionMessage(string input, string hub)
        {
            var hubs = new List<string> { };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var groupName = GenerateRandomName();

            var connectionId = GenerateRandomName();
            var message = CreateMessage(PrefixHelper.GetHubConnectionId(hub + "." + connectionId), input);
            var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
            message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

            var msgs = parser.GetMessages(message).ToList();
            Assert.Single(msgs);
            var msg = msgs[0].Message as ConnectionDataMessage;
            Assert.NotNull(msg);
            Assert.Equal(connectionId, msg.ConnectionId);
            Assert.Equal(input, msg.Payload.First.GetSingleFramePayload());
        }

        [Theory]
        [InlineData("msg", "hub1")]
        [InlineData(null, "hub.hub1")]
        [InlineData("", "hub.hub1.h.hub2")]
        public void TestHubGroupMessage(string input, string hub)
        {
            var hubs = new List<string> { "hub1", "hub.hub1", "hub.hub1.h.hub2" };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var groupName = GenerateRandomName();
            var fullName = PrefixHelper.GetHubGroupName(hub + "." + groupName);
            var message = CreateMessage(fullName, input);
            var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
            message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

            var msgs = parser.GetMessages(message).ToList();
            Assert.Single(msgs);
            var msg = msgs[0].Message as GroupBroadcastDataMessage;
            Assert.NotNull(msg);

            // For group message, it is the full name as the group, e.g. hg-hub.hub1.h.hub2.abcde
            Assert.Equal(fullName, msg.GroupName);
            Assert.Equal<string>(excludedConnectionIds, msg.ExcludedList);
            Assert.Equal(input, msg.Payloads["json"].GetSingleFramePayload());
        }

        [Theory]
        [InlineData("msg", "hub1")]
        [InlineData(null, "hub.hub1")]
        [InlineData("", "hub2.hub1.h.hub2")]
        public void TestHubUserMessage(string input, string hub)
        {
            var hubs = new List<string> { "hub1", "hub.hub1", "hub2.hub1.h.hub2" };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var userName = GenerateRandomName();

            var message = CreateMessage(PrefixHelper.GetHubUserId(hub + "." + userName), input);
            var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
            message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

            var msgs = parser.GetMessages(message).ToList();
            Assert.Single(msgs);
            var msg = msgs[0].Message as UserDataMessage;
            Assert.NotNull(msg);
            Assert.Equal(userName, msg.UserId);
            Assert.Equal(input, msg.Payloads["json"].GetSingleFramePayload());
        }

        [Fact]
        public void TestHubUserMessageWithMultiplePossiblities()
        {
            var hubs = new List<string> { "hub", "hub.hub1", "hub.hub1.h.hub2" };
            var parser = new SignalRMessageParser(hubs, _resolver);
            var fullName = "hub.hub1.h.hub2.user1";
            var message = CreateMessage(PrefixHelper.GetHubUserId(fullName), null);

            var msgs = parser.GetMessages(message).ToList();
            Assert.Equal(3, msgs.Count);
            var msg = msgs[0].Message as UserDataMessage;
            Assert.NotNull(msg);
            Assert.Equal("h.hub2.user1", msg.UserId);
            msg = msgs[1].Message as UserDataMessage;
            Assert.NotNull(msg);
            Assert.Equal("user1", msg.UserId);
            msg = msgs[2].Message as UserDataMessage;
            Assert.NotNull(msg);
            Assert.Equal("hub1.h.hub2.user1", msg.UserId);
        }

        private string GenerateRandomName()
        {
            return Guid.NewGuid().ToString("N");
        }

        private Message CreateMessage(string key, object value)
        {
            ArraySegment<byte> messageBuffer = GetMessageBuffer(value);

            var message = new Message(Guid.NewGuid().ToString("N"), key, messageBuffer);

            var command = value as Command;
            if (command != null)
            {
                // Set the command id
                message.CommandId = command.Id;
                message.WaitForAck = command.WaitForAck;
            }

            return message;
        }

        private ArraySegment<byte> GetMessageBuffer(object value)
        {
            ArraySegment<byte> messageBuffer;
            // We can't use "as" like we do for Command since ArraySegment is a struct
            if (value is ArraySegment<byte>)
            {
                // We assume that any ArraySegment<byte> is already JSON serialized
                messageBuffer = (ArraySegment<byte>)value;
            }
            else
            {
                messageBuffer = SerializeMessageValue(value);
            }
            return messageBuffer;
        }

        private ArraySegment<byte> SerializeMessageValue(object value)
        {
            using (var writer = new MemoryPoolTextWriter(_pool))
            {

                var selfSerializer = value as IJsonWritable;

                if (selfSerializer != null)
                {
                    selfSerializer.WriteJson(writer);
                }
                else
                {
                    _serializer.Serialize(writer, value);
                }

                writer.Flush();

                var data = writer.Buffer;

                var buffer = new byte[data.Count];

                Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);

                return new ArraySegment<byte>(buffer);
            }
        }

        private static IDependencyResolver GetDefaultResolver()
        {
            var config = new HubConfiguration();
            var resolver = config.Resolver;
            resolver.Register(typeof(JsonSerializer), () => new JsonSerializer());
            resolver.Register(typeof(IServiceProtocol), () => new ServiceProtocol());
            resolver.Register(typeof(IMemoryPool), () => new MemoryPool());
            return resolver;
        }

        private static string GetFilter(IList<string> excludedSignals)
        {
            if (excludedSignals != null)
            {
                return String.Join("|", excludedSignals);
            }

            return null;
        }
    }
}