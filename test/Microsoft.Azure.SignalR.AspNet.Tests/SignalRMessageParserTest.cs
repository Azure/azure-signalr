// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

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
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var raw = "<script type=\"\"></script>";
        var message = new Message("foo", key, new ArraySegment<byte>(Encoding.Default.GetBytes(raw)));

        var result = Assert.Throws<NotSupportedException>(() => parser.GetMessages(message).ToList());
    }

    [Fact]
    public void TestAddToGroupCommandMessage()
    {
        var hubs = new List<string> { };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();
        var command = new Command
        {
            CommandType = CommandType.AddToGroup,
            Value = groupName,
            WaitForAck = true
        };

        var connectionId = GenerateRandomName();
        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetConnectionId(connectionId), command);

        var msgs = parser.GetMessages(message).ToList();
        Assert.Single(msgs);
        var msg = Assert.IsType<JoinGroupWithAckMessage>(msgs[0].Message);
        Assert.Equal(connectionId, msg.ConnectionId);
        Assert.Equal(groupName, msg.GroupName);
    }

    [Theory]
    [InlineData(CommandType.AddToGroup, "")]
    [InlineData(CommandType.AddToGroup, "a")]
    [InlineData(CommandType.AddToGroup, "c-")]
    [InlineData(CommandType.AddToGroup, "h-a")]
    [InlineData(CommandType.RemoveFromGroup, "")]
    [InlineData(CommandType.RemoveFromGroup, "a")]
    [InlineData(CommandType.RemoveFromGroup, "c-")]
    [InlineData(CommandType.RemoveFromGroup, "h-a")]
    public void TestAddToGroupCommandMessageWithInvalidKeyThrows(CommandType type, string invalidKey)
    {
        var hubs = new List<string> { };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();
        var command = new Command
        {
            CommandType = type,
            Value = groupName,
            WaitForAck = true
        };

        var message = SignalRMessageUtility.CreateMessage(invalidKey, command);

        Assert.Throws<InvalidDataException>(() => parser.GetMessages(message).ToList());
    }

    [Fact]
    public void TestRemoveFromGroupCommandMessage()
    {
        var hubs = new List<string> { };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();
        var command = new Command
        {
            CommandType = CommandType.RemoveFromGroup,
            Value = groupName,
            WaitForAck = true
        };

        var connectionId = GenerateRandomName();
        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetConnectionId(connectionId), command);

        var msgs = parser.GetMessages(message).ToList();
        Assert.Single(msgs);
        var msg = Assert.IsType<LeaveGroupWithAckMessage>(msgs[0].Message);
        Assert.Equal(connectionId, msg.ConnectionId);
        Assert.Equal(groupName, msg.GroupName);
    }

    [Theory]
    [InlineData(CommandType.Initializing)]
    [InlineData(CommandType.Abort)]
    public void TestOtherGroupCommandMessagesAreIgnored(CommandType type)
    {
        var hubs = new List<string> { };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();
        var command = new Command
        {
            CommandType = type,
            Value = groupName,
            WaitForAck = true
        };

        var connectionId = GenerateRandomName();
        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetConnectionId(connectionId), command);

        var msgs = parser.GetMessages(message).ToList();
        Assert.Empty(msgs);
    }


    [Theory]
    [InlineData("connection1", "msg")]
    [InlineData("a.connection1", null)]
    [InlineData("h-a.connection1", "")]
    [InlineData("h-", "")]
    [InlineData("", "", typeof(NotSupportedException))]
    public void TestHubMessage(string connectionId, string input, Type exceptionType = null)
    {
        var hubs = new List<string> { "h-", "a", "a.connection1" };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();

        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetHubName(connectionId), input);
        var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
        message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

        if (exceptionType != null)
        {
            Assert.Throws(exceptionType, () => parser.GetMessages(message).ToList());
            return;
        }

        var msgs = parser.GetMessages(message).ToList();
        Assert.Single(msgs);
        var msg = msgs[0].Message as BroadcastDataMessage;
        Assert.NotNull(msg);
        Assert.Equal<string>(excludedConnectionIds, msg.ExcludedList);
        Assert.Equal(input, msg.Payloads["json"].GetJsonMessageFromSingleFramePayload<string>());
    }

    [Theory]
    [InlineData("hc-hub.hub1", null, "hub1")]
    [InlineData("hc-hub.hub1.h....user1", null, "user1")]
    [InlineData("hc-hub1.connection1", "", "connection1")]
    [InlineData("hub1.connection1", "", "connection1")]
    [InlineData("key", "", null, typeof(ArgumentException))]
    [InlineData("", "", null, typeof(NotSupportedException))]
    [InlineData("hc-hub1", "msg", null, typeof(ArgumentException))]
    public void TestHubConnectionMessage(string connectionId, string input, string expectedId, Type exceptionType = null)
    {
        var hubs = new List<string> { "hub", "hub1", "hub.hub1", "h", "hub.hub1.h.hub2", "hub.hub1.h" };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();

        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetHubConnectionId(connectionId), input);
        var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
        message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

        if (exceptionType != null)
        {
            Assert.Throws(exceptionType, () => parser.GetMessages(message).ToList());
            return;
        }

        var msgs = parser.GetMessages(message).ToList();
        Assert.Single(msgs);
        var msg = msgs[0].Message as ConnectionDataMessage;
        Assert.NotNull(msg);
        Assert.Equal(expectedId, msg.ConnectionId);
        Assert.Equal(input, msg.Payload.First.GetJsonMessageFromSingleFramePayload<string>());
    }

    [Theory]
    [InlineData("msg", "hub1")]
    [InlineData(null, "hub.hub1")]
    [InlineData("", "hub.hub1.h.hub2")]
    public void TestHubGroupMessage(string input, string hub)
    {
        var hubs = new List<string> { "hub1", "hub.hub1", "hub.hub1.h.hub2" };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var groupName = GenerateRandomName();
        var fullName = PrefixHelper.GetHubGroupName(hub + "." + groupName);
        var message = SignalRMessageUtility.CreateMessage(fullName, input);
        var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
        message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

        var msgs = parser.GetMessages(message).ToList();
        Assert.Single(msgs);
        var msg = msgs[0].Message as GroupBroadcastDataMessage;
        Assert.NotNull(msg);

        // For group message, it is the full name as the group, e.g. hg-hub.hub1.h.hub2.abcde
        Assert.Equal(fullName, msg.GroupName);
        Assert.Equal<string>(excludedConnectionIds, msg.ExcludedList);
        Assert.Equal(input, msg.Payloads["json"].GetJsonMessageFromSingleFramePayload<string>());
    }

    [Theory]
    [InlineData("user", "msg", "hub1")]
    [InlineData(".", null, "hub.hub1")]
    [InlineData("..hub1.user1", "", "hub2.hub1.h.hub2")]
    public void TestHubUserMessage(string userName, string input, string hub, Type exceptionType = null)
    {
        var hubs = new List<string> { "hub1", "hub.hub1", "hub2.hub1.h.hub2", ".", ".." };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);

        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetHubUserId(hub + "." + userName), input);
        var excludedConnectionIds = new string[] { GenerateRandomName(), GenerateRandomName() };
        message.Filter = GetFilter(excludedConnectionIds.Select(s => PrefixHelper.GetConnectionId(s)).ToList());

        if (exceptionType != null)
        {
            Assert.Throws(exceptionType, () => parser.GetMessages(message).ToList());
            return;
        }

        var msgs = parser.GetMessages(message).ToList();
        Assert.Single(msgs);
        var msg = msgs[0].Message as UserDataMessage;
        Assert.NotNull(msg);
        Assert.Equal(userName, msg.UserId);
        Assert.Equal(input, msg.Payloads["json"].GetJsonMessageFromSingleFramePayload<string>());
    }

    [Fact]
    public void TestHubUserMessageWithMultiplePossiblities()
    {
        var hubs = new List<string> { "hub", "hub.hub1", "hub.hub1.h.hub2", ".", "......" };
        var parser = new SignalRMessageParser(hubs, _resolver, NullLogger<SignalRMessageParser>.Instance);
        var fullName = "hub.hub1.h.hub2.user1";
        var message = SignalRMessageUtility.CreateMessage(PrefixHelper.GetHubUserId(fullName), null);

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
            return string.Join("|", excludedSignals);
        }

        return null;
    }
}