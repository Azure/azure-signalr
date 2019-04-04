// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal static class AuthorizeHelper
    {
        public static async Task<bool> AuthorizeAsync(HttpContext context, IList<IAuthorizeData> policies)
        {
            if (policies != null && policies.Count == 0)
            {
                return true;
            }

            var policyProvider = context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();

            var authorizePolicy = await AuthorizationPolicy.CombineAsync(policyProvider, policies);

            var policyEvaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();

            // This will set context.User if required
            var authenticateResult = await policyEvaluator.AuthenticateAsync(authorizePolicy, context);

            var authorizeResult = await policyEvaluator.AuthorizeAsync(authorizePolicy, authenticateResult, context, resource: null);
            if (authorizeResult.Succeeded)
            {
                return true;
            }
            else if (authorizeResult.Challenged)
            {
                if (authorizePolicy.AuthenticationSchemes.Count > 0)
                {
                    foreach (var scheme in authorizePolicy.AuthenticationSchemes)
                    {
                        await context.ChallengeAsync(scheme);
                    }
                }
                else
                {
                    await context.ChallengeAsync();
                }
                return false;
            }
            else if (authorizeResult.Forbidden)
            {
                if (authorizePolicy.AuthenticationSchemes.Count > 0)
                {
                    foreach (var scheme in authorizePolicy.AuthenticationSchemes)
                    {
                        await context.ForbidAsync(scheme);
                    }
                }
                else
                {
                    await context.ForbidAsync();
                }
                return false;
            }
            return false;
        }

        public static List<IAuthorizeData> BuildAuthorizePolicy(Type hub)
        {
#if NETCOREAPP3_0
            // Core 3.0 is using AuthorizationMiddleware to handle this, no need to do again under Azure SignalR.
            return null;
#else
            var authorizeAttributes = hub.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
            var authorizeData = new List<IAuthorizeData>();
            foreach (var attribute in authorizeAttributes)
            {
                authorizeData.Add((AuthorizeAttribute)attribute);
            }
            return authorizeData;
#endif
        }
    }
}
