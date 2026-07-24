// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management;

/// <summary>
/// Configures a live connection authentication refresh operation.
/// </summary>
public sealed class RefreshConnectionAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the new application authentication expiration deadline. When null, the existing deadline is preserved.
    /// </summary>
    public DateTimeOffset? AuthenticationExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the projected application claims. When null, the existing claims are preserved.
    /// </summary>
    public IEnumerable<Claim> Claims { get; set; }

    /// <summary>
    /// Gets or sets the maximum lifetime of the refreshed service access token. Default value is one hour.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = Constants.Periods.DefaultAccessTokenLifetime;
}
