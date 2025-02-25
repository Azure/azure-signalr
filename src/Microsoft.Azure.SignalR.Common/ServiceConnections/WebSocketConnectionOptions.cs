// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class WebSocketConnectionOptions
{
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    public X509CertificateCollection ClientCertificates { get; set; } = new X509CertificateCollection();

    public CookieContainer Cookies { get; set; } = new CookieContainer();

    public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public ICredentials? Credentials { get; set; }

    public IWebProxy? Proxy { get; set; }

    public bool? UseDefaultCredentials { get; set; }

    public Action<ClientWebSocketOptions>? WebSocketConfiguration { get; set; }
}
