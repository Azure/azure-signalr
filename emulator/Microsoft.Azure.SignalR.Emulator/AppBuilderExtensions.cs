// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal static class AppBuilderExtensions
    {
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
            var accessKey = configuration.GetValue<string>("AccessKey");
            var keyBytes = string.IsNullOrEmpty(accessKey) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(accessKey);
            var key = new SecurityKey[] { new SymmetricSecurityKey(keyBytes) };
            
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = false,
                    IssuerSigningKeyResolver = (t, s, k, v) => key,
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
            services.AddSingleton<DynamicHubContextStore>();

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
    }
}
