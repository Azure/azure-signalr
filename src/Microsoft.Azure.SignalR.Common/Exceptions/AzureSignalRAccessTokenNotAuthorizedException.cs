// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using Azure.Core;

namespace Microsoft.Azure.SignalR.Common;

#nullable enable

/// <summary>
/// The exception throws when AccessKey is not authorized.
/// </summary>
public class AzureSignalRAccessTokenNotAuthorizedException : AzureSignalRException
{
    private const string Template = "{0} is not available for signing client tokens, {1}";

    /// <summary>
    /// Obsolete, <see cref="AzureSignalRAccessTokenNotAuthorizedException(TokenCredential, string, Exception?)"/>.
    /// </summary>
    /// <param name="message"></param>
    [Obsolete]
    public AzureSignalRAccessTokenNotAuthorizedException(string message) : base(message)
    {
    }

    /// <summary>
    /// Obsolete, <see cref="AzureSignalRAccessTokenNotAuthorizedException(TokenCredential, string, Exception?)"/>.
    /// </summary>
    /// <param name="credentialName"></param>
    /// <param name="inner"></param>
    [Obsolete]
    public AzureSignalRAccessTokenNotAuthorizedException(string credentialName, Exception inner) :
        base(string.Format(CultureInfo.InvariantCulture, Template, credentialName, AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra), inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSignalRAccessTokenNotAuthorizedException"/> class.
    /// </summary>
    internal AzureSignalRAccessTokenNotAuthorizedException(TokenCredential credential, string message, Exception? inner = null) :
        base(string.Format(CultureInfo.InvariantCulture, Template, credential.GetType().Name, message), inner)
    {
    }
}
