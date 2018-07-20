// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.AspNet.SignalR.Infrastructure
{
    /// <summary>
    /// Copied from https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Infrastructure/ListHelper.cs
    /// </summary>
    internal class ListHelper<T>
    {
        public static readonly IList<T> Empty = new ReadOnlyCollection<T>(new List<T>());
    }
}
