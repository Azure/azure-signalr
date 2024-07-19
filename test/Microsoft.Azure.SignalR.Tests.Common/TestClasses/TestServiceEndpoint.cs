using System;
using Azure.Core;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestServiceEndpoint : ServiceEndpoint
{
    private static Uri DefaultEndpoint = new Uri("https://localhost");

    private const string _defaultConnectionString = "Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ;Version=1.0";

    public TestServiceEndpoint(string name = "", string connectionString = null) : base(connectionString ?? _defaultConnectionString, name: name)
    {
    }

    public TestServiceEndpoint(TokenCredential tokenCredential) : base(DefaultEndpoint, tokenCredential)
    {
    }
}