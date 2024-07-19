using System;
using Azure.Core;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestServiceEndpoint : ServiceEndpoint
{
    private const string DefaultConnectionString = "Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ;Version=1.0";

    private static readonly Uri DefaultEndpoint = new Uri("https://localhost");

    public TestServiceEndpoint(string name = "", string connectionString = null) : base(connectionString ?? DefaultConnectionString, name: name)
    {
    }

    public TestServiceEndpoint(TokenCredential tokenCredential) : base(DefaultEndpoint, tokenCredential)
    {
    }
}