// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management;

public class NegotiationOptions
{
    internal static readonly NegotiationOptions Default = new NegotiationOptions();

    /// <summary>
    /// Gets or sets the HTTP context object that might provide information for routing and generating access token.
    /// </summary>
    public HttpContext HttpContext { get; set; }

    /// <summary>
    /// Gets or sets the user ID. If null, the identity name in <see cref="HttpContext.User" /> of the property <see cref="HttpContext"/> will be used.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the claim list to be put into access token. If null, the claims in <see cref="HttpContext.User"/> of the property <see cref="HttpContext"/> will be used.
    /// </summary>
    public IList<Claim> Claims { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of <see cref="NegotiationResponse.AccessToken"/>. Default value is one hour.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the flag indicates whether the client is a diagnostic client.
    /// </summary>
    public bool IsDiagnosticClient { get; set; } = false;

    /// <summary>
    /// Gets or sets the flag indicates whether detailed errors are logged in the client side.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets the flag indicates that whether the connection should be closed when the authentication token expires. The lifetime of the token is determined by <see cref="TokenLifetime"/>.
    /// </summary>
    public bool CloseOnAuthenticationExpiration { get; set; } = false;
}