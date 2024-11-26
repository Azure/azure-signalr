// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal enum AckStatus
{
    Ok = 1,

    NotFound = 2,

    Timeout = 3,

    InternalServerError = 4,
}
