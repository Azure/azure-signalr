// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class JsonPayloadMessageContentTest
    {
        [Theory]
        [MemberData(nameof(TestData))]
        internal async Task TestSerialization(ObjectSerializer objectSerializer, PayloadMessage payloadMessage, string jsonString)
        {
            var httpContent = new JsonPayloadMessageContent(payloadMessage, objectSerializer);
            var outputStream = new MemoryStream();
            await httpContent.CopyToAsync(outputStream);
            outputStream.Seek(0, SeekOrigin.Begin);
            var actualJsonString = new StreamReader(outputStream).ReadToEnd();
            Assert.Equal(jsonString, actualJsonString);
        }

        public static IEnumerable<object[]> TestData =>
            from objectSeralizer in new ObjectSerializer[] { new JsonObjectSerializer(), new NewtonsoftJsonObjectSerializer() }
            from pair in ArgumentsAndString
            select new object[] { objectSeralizer, new PayloadMessage
                {
                    Target = "target",
                    Arguments = pair.Arguments
                },  pair.Json};

        public static IEnumerable<(object[] Arguments, string Json)> ArgumentsAndString
        {
            get
            {
                yield return (null, "{\"Target\":\"target\",\"Arguments\":null}");
                yield return (Array.Empty<object>(), "{\"Target\":\"target\",\"Arguments\":[]}");
                yield return (new object[] { null, false, "string", new { Name = "name" } }, "{\"Target\":\"target\",\"Arguments\":[null,false,\"string\",{\"Name\":\"name\"}]}");
            }
        }
    }
}
