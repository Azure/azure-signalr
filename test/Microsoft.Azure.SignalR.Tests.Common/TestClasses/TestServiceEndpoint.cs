using System;
using Azure.Core;

namespace Microsoft.Azure.SignalR.Tests.Common;

#nullable enable

internal class TestServiceEndpoint : ServiceEndpoint
{
    private static Uri DefaultEndpoint = new Uri("https://localhost");

    private const string DefaultConnectionString = "Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ;Version=1.0";

    public TestServiceEndpoint(string name = "", string? connectionString = null) : base(connectionString ?? DefaultConnectionString, name: name)
    {
    }

    public TestServiceEndpoint(TokenCredential tokenCredential) : base(DefaultEndpoint, tokenCredential)
    {
    }
}