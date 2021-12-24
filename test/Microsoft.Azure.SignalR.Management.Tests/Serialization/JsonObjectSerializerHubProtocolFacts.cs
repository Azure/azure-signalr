// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.SignalR.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class JsonObjectSerializerHubProtocolFacts
    {
        public readonly ITestOutputHelper testOutputHelper;

        public JsonObjectSerializerHubProtocolFacts(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public static IEnumerable<object[]> HubMessageTestData
        {
            get
            {
                yield return new object[] { new InvocationMessage("target", Array.Empty<object>()) };
                yield return new object[] { new InvocationMessage("target", new object[] { null, null }) };
                yield return new object[] { new InvocationMessage("target", new object[] { "string", true }) };
                yield return new object[] { new InvocationMessage("invocationId", "target", new object[] { "string", true, new { name = "abc", value = 3 }, new object[] { 1, 2, 3 } }, new string[] { "streamId1" }) };

                yield return new object[] { new StreamInvocationMessage(null, "target", Array.Empty<object>()) };
                yield return new object[] { new StreamInvocationMessage(null, "target", new object[] { null, null }) };
                yield return new object[] { new StreamInvocationMessage(null, "target", new object[] { "string", true }) };
                yield return new object[] { new StreamInvocationMessage("invocationId", "target", new object[] { "string", true }, new string[] { "streamId1" }) };

                yield return new object[] { new StreamItemMessage(null, null) };
                yield return new object[] { new StreamItemMessage(null, "string") };
                yield return new object[] { new StreamItemMessage(null, Array.Empty<object>()) };
                yield return new object[] { new StreamItemMessage("invocationId", new object[] { 1, 2, 3 }) };

                yield return new object[] { new CompletionMessage(null, null, null, false) };
                yield return new object[] { new CompletionMessage(null, null, null, true) };
                yield return new object[] { new CompletionMessage(null, null, Array.Empty<object>(), true) };
                yield return new object[] { new CompletionMessage("invocationId", null, new object[] { 1, 2, 3 }, true) };
            }
        }

        [Theory]
        [MemberData(nameof(HubMessageTestData))]
        public void TestSerializeHubMessage(HubMessage message)
        {
            var testProtocol = new JsonObjectSerializerHubProtocol();
            var baseProtocol = new JsonHubProtocol();
            var testBytes = testProtocol.GetMessageBytes(message);
            var baseBytes = baseProtocol.GetMessageBytes(message);

            testOutputHelper.WriteLine(Encoding.UTF8.GetString(testBytes.Span));
            testOutputHelper.WriteLine(Encoding.UTF8.GetString(baseBytes.Span));

            Assert.True(testBytes.Span.SequenceEqual(baseBytes.Span));
        }
    }
}
