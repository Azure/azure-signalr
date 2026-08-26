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
    /// Gets or sets the application authentication expiration deadline. When null and <see cref="CloseOnAuthenticationExpiration"/> is enabled,
    /// the deadline defaults to the current time plus <see cref="TokenLifetime"/> for backward compatibility.
    /// </summary>
    public DateTimeOffset? AuthenticationExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the flag indicates whether the client is a diagnostic client. By default it is false.
    /// </summary>
    public bool IsDiagnosticClient { get; set; }

    /// <summary>
    /// Gets or sets the flag indicates whether detailed errors are logged in the client side. By default it is false.
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// Gets or sets the flag indicates whether the connection should be closed at <see cref="AuthenticationExpiresOn"/>. By default it is false.
    /// </summary>
    public bool CloseOnAuthenticationExpiration { get; set; }

    /// <summary>
    /// Gets or sets a bitmask combining one or more <see cref="HttpTransportType"/> values that specify what transports the server should use to receive HTTP requests.
    /// </summary>
    public HttpTransportType? Transports { get; set; }
}
