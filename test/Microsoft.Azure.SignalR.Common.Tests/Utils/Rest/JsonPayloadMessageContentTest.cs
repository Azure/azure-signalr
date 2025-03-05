// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Azure.Core.Serialization;

using Microsoft.AspNetCore.SignalR.Protocol;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class JsonPayloadMessageContentTest
{
    [Theory]
    [MemberData(nameof(GetInvocationData))]
    [MemberData(nameof(GetStreamItemData))]
    internal async Task TestSerialization(ObjectSerializer objectSerializer, HubMessage payloadMessage, string jsonString)
    {
        var httpContent = new JsonPayloadMessageContent(payloadMessage, objectSerializer, typeof(object));
        var outputStream = new MemoryStream();
        await httpContent.CopyToAsync(outputStream);
        outputStream.Seek(0, SeekOrigin.Begin);
        var actualJsonString = new StreamReader(outputStream).ReadToEnd();
        Assert.Equal(jsonString, actualJsonString);
    }

    public static IEnumerable<object[]> GetInvocationData =>
        from objectSeralizer in new ObjectSerializer[] { new JsonObjectSerializer(), new NewtonsoftJsonObjectSerializer() }
        from pair in GetInvocationArgumentsAndString()
        select new object[] { objectSeralizer, new InvocationMessage("target", pair.Arguments), pair.Json };

    private static IEnumerable<(object[] Arguments, string Json)> GetInvocationArgumentsAndString()
    {
        yield return (null, """{"Target":"target","Arguments":null}""");
        yield return (Array.Empty<object>(), """{"Target":"target","Arguments":[]}""");
        yield return (new object[] { null, false, "string", new { Name = "name" } }, """{"Target":"target","Arguments":[null,false,"string",{"Name":"name"}]}""");
    }

    public static IEnumerable<object[]> GetStreamItemData() =>
        from objectSeralizer in new ObjectSerializer[] { new JsonObjectSerializer(), new NewtonsoftJsonObjectSerializer() }
        from pair in GetStreamItemArgumentAndString()
        select new object[] { objectSeralizer, new StreamItemMessage("id", pair.Argument), pair.Json };

    private static IEnumerable<(object Argument, string Json)> GetStreamItemArgumentAndString()
    {
        yield return (null, "null");
        yield return (new { a = 1 }, """{"a":1}""");
        yield return (Array.Empty<object>(), "[]");
        yield return (new object[] { null, false, "string", new { Name = "name" } }, """[null,false,"string",{"Name":"name"}]""");
    }
}
