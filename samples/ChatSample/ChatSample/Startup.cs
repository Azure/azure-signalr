// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Azure.Core;
using Azure.Identity;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChatSample;

public class Startup
{
    private const AuthTypes AuthType = AuthTypes.VisualStudio;

    /// <summary>
    /// The Endpoint of an Azure SignalR resource.
    /// Can be found in the Overview page.
    /// </summary>
    private const string Endpoint = "https://<resource-name>.service.signalr.net";

    /// <summary>
    /// Should be the Directory (tenant) ID of your Azure SignalR resource is in.
    /// In most cases, it should be the same with the application Tenant ID.
    /// </summary>
    private const string TenantId = "";

    /// <summary>
    /// Should be the Application (client) ID of a app registrations.
    /// Or the Application ID of an enterprise application that you provisioned from other tenants.
    /// </summary>
    private const string AppClientId = "";

    /// <summary>
    /// Should be the Client ID of a user-assigned managed identity, not Object (principal) ID!
    /// </summary>
    private const string MsiClientId = "";

    private enum AuthTypes
    {
        VisualStudio = 0,

        ApplicationWithCertificate,

        ApplicationWithClientSecret,

        ApplicationWithFederatedIdentity,

        SystemAssignedManagedIdentity,

        UserAssignedManagedIdentity,
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc();
        services.AddSignalR()
            .AddAzureSignalR(option =>
            {
                TokenCredential credential = AuthType switch
                {
                    AuthTypes.VisualStudio => new VisualStudioCodeCredential(),
                    AuthTypes.ApplicationWithCertificate => new ClientCertificateCredential(TenantId, AppClientId, "path-to-cert-file"),
                    AuthTypes.ApplicationWithClientSecret => new ClientSecretCredential(TenantId, AppClientId, "client-secret-value"),
                    AuthTypes.ApplicationWithFederatedIdentity => GetClientAssertionCredential(TenantId, AppClientId, MsiClientId),
                    AuthTypes.SystemAssignedManagedIdentity => new ManagedIdentityCredential(),
                    AuthTypes.UserAssignedManagedIdentity => new ManagedIdentityCredential(MsiClientId),
                    _ => throw new NotImplementedException(),
                };

                option.Endpoints = [
                    new ServiceEndpoint(new Uri(Endpoint), credential)
                ];
            })
            .AddMessagePackProtocol();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseFileServer();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(routes =>
        {
            routes.MapHub<ChatHub>("/chat");
            routes.MapHub<BenchHub>("/signalrbench");
        });
    }

    private static ClientAssertionCredential GetClientAssertionCredential(string appTenantId, string appClientId, string msiClientId)
    {
        var msiCredential = new ManagedIdentityCredential(msiClientId);

        return new ClientAssertionCredential(appTenantId, appClientId, async (ctoken) =>
        {
            // Entra ID US Government: api://AzureADTokenExchangeUSGov
            // Entra ID China operated by 21Vianet: api://AzureADTokenExchangeChina
            var request = new TokenRequestContext([$"api://AzureADTokenExchange/.default"]);
            var response = await msiCredential.GetTokenAsync(request, ctoken).ConfigureAwait(false);
            return response.Token;
        });
    }
}
