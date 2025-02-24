// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR;

public interface ICustomHeaderProvider
{
    IDictionary<string, string> GetCustomHeaders();
}
