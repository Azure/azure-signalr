// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.SignalR
{
    internal class WebSocketConnectionOptions
    {
        public WebSocketConnectionOptions()
        {
            Headers = new Dictionary<string, string>();
            ClientCertificates = new X509CertificateCollection();
            Cookies = new CookieContainer();
            CloseTimeout = TimeSpan.FromSeconds(5);
        }

        public IDictionary<string, string> Headers { get; set; }

        public X509CertificateCollection ClientCertificates { get; set; }
       
        public CookieContainer Cookies { get; set; }
       
        public TimeSpan CloseTimeout { get; set; }
        
        public ICredentials Credentials { get; set; }
        
        public IWebProxy Proxy { get; set; }
      
        public bool? UseDefaultCredentials { get; set; }
      
        public Action<ClientWebSocketOptions> WebSocketConfiguration { get; set; }

        public AuthType AuthType { get; set; }
    }
}
