// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    using ServicePingMessage = Microsoft.Azure.SignalR.Protocol.PingMessage;

    public class ServiceStatusPingMessage
    {
        public static readonly ServicePingMessage ActiveServicePingMessage = new ServiceStatusPingMessage(true).ToServicePingMessage();

        private readonly string _status;

        public const string Key = "status";

        public bool IsActive { get; }

        public ServiceStatusPingMessage(string status)
        {
            _status = status;
            IsActive = status == "1";
        }

        public ServiceStatusPingMessage(bool isActive)
        {
            IsActive = isActive;
            _status = IsActive ? "1" : "0";
        }

        public ServicePingMessage ToServicePingMessage()
        {
            return new ServicePingMessage { Messages = new[] { Key, _status } };
        }
    }
}
