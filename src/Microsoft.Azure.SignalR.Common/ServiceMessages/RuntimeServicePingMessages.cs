using System;
using System.Collections.Generic;
using ServicePingMessage = Microsoft.Azure.SignalR.Protocol.PingMessage;

namespace Microsoft.Azure.SignalR
{
    public static class RuntimeServicePingMessage
    {
        private const string OfflineKey = "offline";
        private const string TargetKey = "target";
        private const string StatusKey = "status";
        private const string ShutdownKey = "shutdown";
        private const string ServersKey = "servers";

        private const string StatusActiveValue = "1";
        private const string StatusInactiveValue = "0";
        private const string ShutdownFinValue = "fin";
        private const string ShutdownFinAckValue = "finack";
        private const char ServerListSeparator = ';';

        private static readonly ServicePingMessage StatusActive =
            new ServicePingMessage { Messages = new[] { StatusKey, StatusActiveValue } };

        private static readonly ServicePingMessage StatusInactive =
            new ServicePingMessage { Messages = new[] { StatusKey, StatusInactiveValue } };

        private static readonly ServicePingMessage ShutdownFin =
            new ServicePingMessage { Messages = new[] { ShutdownKey, ShutdownFinValue } };

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

        public static bool TryGetServerIds(this ServicePingMessage ping, out HashSet<string> serverIds, out long updatedTime)
        {
            if (!TryGetValue(ping, ServersKey, out var value) || string.IsNullOrEmpty(value) 
                || long.TryParse(value.Substring(0, value.IndexOf(":")), out updatedTime))
            {
                serverIds = null;
                updatedTime = DateTime.MinValue.Ticks;
                return false;
            }
            var servers = value.Substring(value.IndexOf(":") + 1);
            serverIds = new HashSet<string>(servers.Split(new char[] { ServerListSeparator }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }

        public static ServicePingMessage GetFinPingMessage() => ShutdownFin;

        public static ServicePingMessage GetServersPingMessage() => GetServerIds;

        public static bool IsFinAck(this ServicePingMessage ping) =>
            TryGetValue(ping, ShutdownKey, out var value) && value == ShutdownFinAckValue;

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
