// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Owin;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal sealed class TestAppBuilder : IAppBuilder
{
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>()
    {
        ["builder.AddSignatureConversion"] = new Action<Delegate>(e => { })
    };

    public object Build(Type returnType)
    {
        return null;
    }

    public IAppBuilder New()
    {
        return null;
    }

    public IAppBuilder Use(object middleware, params object[] args)
    {
        return this;
    }
}
