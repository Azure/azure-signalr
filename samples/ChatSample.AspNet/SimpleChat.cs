// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace ChatSample.AspNet
{
    [HubName("Chat.A")]
    [Authorize]
    public class SimpleChat : Hub
    {
        public override async Task OnConnected()
        {
            await base.OnConnected();
        }

        [Authorize(Roles = "Admin")]
        public void Hello(string message)
        {
            string name;
            var user = Context.User;
            if (user.Identity.IsAuthenticated)
            {
                name = user.Identity.Name;
            }
            else
            {
                var role = ((ClaimsIdentity)user.Identity).Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value);

                throw new UnauthorizedAccessException($"User is in role {role}");
            }

            Clients.Caller.hello("Successfully sent", name);

            for (int i = 0; i < 10; i++)
            {
                Clients.All.hello(name, $"round {i}: {message}");
            }
        }
    }
}