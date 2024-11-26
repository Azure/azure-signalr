// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal interface IAccessTokenProvider
{
    Task<string> ProvideAsync();
}
