// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerContext
    {
        public string ProductInfo { get; set; }

        public ServiceEndpoint[] ServiceEndpoints { get; set; }

        public string ApplicationName { get; set; }

        public int ConnectionCount { get; set; }

        public IWebProxy Proxy { get; set; }

        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        public bool DisposeServiceProvider { get; set; } = false;

        internal void ValidateOptions()
        {
            ValidateServiceEndpoint();
            ValidateServiceTransportType();
        }

        private void ValidateServiceEndpoint()
        {
            if (ServiceEndpoints.Length == 0)
            {
                throw new InvalidOperationException($"Service endpoint(s) is/are not configured.");
            }
        }

        private void ValidateServiceTransportType()
        {
            if (!Enum.IsDefined(typeof(ServiceTransportType), ServiceTransportType))
            {
                throw new ArgumentOutOfRangeException($"Not supported service transport type. " +
                    $"Supported transport types are {ServiceTransportType.Transient} and {ServiceTransportType.Persistent}.");
            }
        }
    }
}