// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal static class AppBuilderExtensions
    {
        internal const string AccessKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH";
        private static readonly SecurityKey[] ValidKeys = new SecurityKey[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AccessKey)) };
        private static readonly MediaTypeHeaderValue EventStreamMediaType = new MediaTypeHeaderValue("text/event-stream");
        private const string AllowAllCors = nameof(AllowAllCors);

        public static IServiceCollection AddAllowAllCors(this IServiceCollection services)
        {
            // https://github.com/dotnet/aspnetcore/issues/4457#issuecomment-465669576
            services.AddCors(options => options.AddPolicy(AllowAllCors,
                builder =>
                {
                    builder.AllowAnyHeader()
                           .AllowAnyMethod()
                           .SetIsOriginAllowed((host) => true)
                           .AllowCredentials();
                }));

            return services;
        }

        public static IServiceCollection AddJwtBearerAuth(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = false,
                    IssuerSigningKeyResolver = (t, s, k, v) => ValidKeys,
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = GetAccessTokenFromQueryString(context.HttpContext);
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }

        public static IServiceCollection AddSignalREmulator(this IServiceCollection services)
        {
            services.AddSingleton(typeof(HubLifetimeManager<>), typeof(CachedHubLifetimeManager<>));
            services.AddSingleton(typeof(HubProxyHandler<>));
            services.AddSingleton<IUserIdProvider, AzureSignalRCustomUserIdProvider>();
            services.AddSingleton(typeof(HttpServerlessMessageHandler<>));
            services.AddSingleton<IHttpUpstreamTrigger, HttpUpstreamTrigger>();
            services.AddSingleton<IDynamicHubContextStore, DynamicHubContextStore>();
            services.AddSignalR().AddMessagePackProtocol();
            return services;
        }

        public static void UseAllowAllCors(this IApplicationBuilder app)
        {
            app.UseCors(AllowAllCors);
        }

        private static string GetAccessTokenFromQueryString(HttpContext context)
        {
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                return null;
            }
            if (context.WebSockets.IsWebSocketRequest || context.Request.GetTypedHeaders().Accept?.Contains(EventStreamMediaType) == true)
            {
                if (context.Request.Query.TryGetValue("access_token", out var accessToken) &&
                    accessToken.Count > 0)
                {
                    return accessToken[accessToken.Count - 1];
                }
            }

            return null;
        }

        private sealed class AzureSignalRCustomUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection)
            {
                return connection.Features.GetUserIdentifier();
            }
        }
    }
}
