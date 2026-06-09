// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// IRequestCultureFeature is unavailable in net462. See https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.localization.irequestculturefeature#applies-to
#if NETSTANDARD2_0 || NET6_0_OR_GREATER
using Microsoft.AspNetCore.Localization;

namespace Microsoft.Azure.SignalR;

internal interface ICultureFeatureManager
{
    bool TryAddCultureFeature(string clientRequestId, IRequestCultureFeature cultureFeature);

    bool TryRemoveCultureFeature(string clientRequestId, out IRequestCultureFeature feature);

    bool IsDefaultFeature(IRequestCultureFeature feature);

    public void Cleanup();
}
#endif
