// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.SignalR.Protocol;
using ServicePingMessage = Microsoft.Azure.SignalR.Protocol.PingMessage;

namespace Microsoft.Azure.SignalR
{
    internal static class RuntimeServicePingMessage
    {
        private const string OfflineKey = "offline";
        private const string TargetKey = "target";
        private const string StatusKey = "status";
        private const string ShutdownKey = "shutdown";
        private const string ServersKey = "servers";

        private const string StatusActiveValue = "1";
        private const string StatusInactiveValue = "0";
        private const string ShutdownFinMigratableValue = "fin:1";
        private const string ShutdownFinValue = "fin:0";
        private const string ShutdownFinAckValue = "finack";
        private const char ServerListSeparator = ';';

        private static readonly ServicePingMessage StatusActive =
            new ServicePingMessage { Messages = new[] { StatusKey, StatusActiveValue } };

        private static readonly ServicePingMessage StatusInactive =
            new ServicePingMessage { Messages = new[] { StatusKey, StatusInactiveValue } };

        private static readonly ServicePingMessage ShutdownFin =
            new ServicePingMessage { Messages = new[] { ShutdownKey, ShutdownFinValue } };

        private static readonly ServicePingMessage ShutdownFinMigratable =
            new ServicePingMessage { Messages = new[] { ShutdownKey, ShutdownFinMigratableValue } };

        private static readonly ServicePingMessage ShutdownFinAck =
            new ServicePingMessage { Messages = new[] { ShutdownKey, ShutdownFinAckValue } };

        private static readonly ServicePingMessage GetServerIds =
            new ServicePingMessage { Messages = new[] { ServersKey, string.Empty } };

        public static bool TryGetOffline(this ServicePingMessage ping, out string instanceId) =>
            TryGetValue(ping, OfflineKey, out instanceId);

        public static bool TryGetRebalance(this ServicePingMessage ping, out string target) =>
            TryGetValue(ping, TargetKey, out target);

        // ping to runtime ask for status
        public static ServicePingMessage GetStatusPingMessage(bool isActive) =>
            isActive ? StatusActive : StatusInactive;

        public static bool TryGetStatus(this ServicePingMessage ping, out bool isActive)
        {
            if (!TryGetValue(ping, StatusKey, out var value))
            {
                isActive = false;
                return false;
            }
            isActive = value == StatusActiveValue;
            return true;
        }

        public static bool TryGetServerIds(this ServicePingMessage ping, out string serverIds, out long updatedTime)
        {
            // servers ping format: { "servers", "1234567890:server1;server2;server3" }
            if (!TryGetValue(ping, ServersKey, out var value) || string.IsNullOrEmpty(value)
                || !long.TryParse(value.Substring(0, value.IndexOf(":")), out updatedTime))
            {
                serverIds = string.Empty;
                updatedTime = DateTime.MinValue.Ticks;
                return false;
            }
            serverIds = value.Substring(value.IndexOf(":") + 1);
            return true;
        }

        public static ServicePingMessage GetFinPingMessage(bool migratable) =>
            migratable ? ShutdownFinMigratable : ShutdownFin;

        public static ServicePingMessage GetFinAckPingMessage() => ShutdownFinAck;

        public static ServicePingMessage GetServersPingMessage() => GetServerIds;

        // for test
        public static bool IsFin(this ServiceMessage serviceMessage) =>
            serviceMessage is ServicePingMessage ping && TryGetValue(ping, ShutdownKey, out var value) && (value == ShutdownFinValue || value == ShutdownFinMigratableValue);

        public static bool IsFinAck(this ServicePingMessage ping) =>
            TryGetValue(ping, ShutdownKey, out var value) && value == ShutdownFinAckValue;

        // for test
        public static bool IsGetServers(this ServiceMessage serviceMessage) =>
            serviceMessage is ServicePingMessage ping && TryGetValue(ping, ServersKey, out _);

        internal static bool TryGetValue(ServicePingMessage pingMessage, string key, out string value)
        {
            if (pingMessage == null)
            {
                value = null;
                return false;
            }

            for (int i = 0; i < pingMessage.Messages.Length - 1; i += 2)
            {
                if (pingMessage.Messages[i] == key)
                {
                    value = pingMessage.Messages[i + 1];
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
