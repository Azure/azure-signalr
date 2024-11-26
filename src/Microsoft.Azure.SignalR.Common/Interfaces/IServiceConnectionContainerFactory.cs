// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR;

internal interface IServiceConnectionContainerFactory
{
    IServiceConnectionContainer Create(string hub, TimeSpan? serviceScaleTimeout = null);
}
