// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal class ClientProxyFactory
    {
        public static ClientProxy CreateAllClientsProxy(IHubMessageSender hubMessageSender, string hubName)
        {
            return new ClientProxy(hubMessageSender, $"/hub/{hubName}");
        }

        public static ClientProxy CreateAllClientsExceptProxy(IHubMessageSender hubMessageSender,
            string hubName, IReadOnlyList<string> excludedList)
        {
            return new ClientProxy(hubMessageSender, $"/hub/{hubName}", excludedList);
        }

        public static ClientProxy CreateSingleClientProxy(IHubMessageSender hubMessageSender, string hubName,
            string connectionId)
        {
            var path = $"/hub/{hubName}/connection/{connectionId}";
            return new ClientProxy(hubMessageSender, path);
        }

        public static ClientProxy CreateMultipleClientProxy(IHubMessageSender hubMessageSender, string hubName,
            IReadOnlyList<string> connectionIds)
        {
            var path = $"/hub/{hubName}/connections/{string.Join(",", connectionIds)}";
            return new ClientProxy(hubMessageSender, path);
        }

        public static ClientProxy CreateSingleUserProxy(IHubMessageSender hubMessageSender, string hubName,
            string userId)
        {
            var path = $"/hub/{hubName}/user/{userId}";
            return new ClientProxy(hubMessageSender, path);
        }

        public static ClientProxy CreateMultipleUserProxy(IHubMessageSender hubMessageSender, string hubName,
            IReadOnlyList<string> userIds)
        {
            var path = $"/hub/{hubName}/users/{string.Join(",", userIds)}";
            return new ClientProxy(hubMessageSender, path);
        }

        public static ClientProxy CreateSingleGroupProxy(IHubMessageSender hubMessageSender, string hubName,
            string groupName)
        {
            var path = $"/hub/{hubName}/group/{groupName}";
            return new ClientProxy(hubMessageSender, path);
        }

        public static ClientProxy CreateMultipleGroupProxy(IHubMessageSender hubMessageSender, string hubName,
            IReadOnlyList<string> groupNames)
        {
            var path = $"/hub/{hubName}/groups/{string.Join(",", groupNames)}";
            return new ClientProxy(hubMessageSender, path);
        }

        public static ClientProxy CreateSingleGroupExceptProxy(IHubMessageSender hubMessageSender, string hubName,
            string groupName, IReadOnlyList<string> excludedList)
        {
            var path = $"/hub/{hubName}/group/{groupName}";
            return new ClientProxy(hubMessageSender, path, excludedList);
        }
    }
}
