// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Microsoft.AspNet.SignalR
{
    /// <summary>
    /// Copied from https://github.com/SignalR/SignalR/blob/dev/src/Microsoft.AspNet.SignalR.Core/Owin/Infrastructure/OwinEnvironmentExtensions.cs
    /// </summary>
    internal static class OwinEnvironmentExtensions
    {
        internal static TextWriter GetTraceOutput(this IDictionary<string, object> environment)
        {
            object value;
            if (environment.TryGetValue(OwinConstants.HostTraceOutputKey, out value))
            {
                return value as TextWriter;
            }

            return null;
        }
    }
}
