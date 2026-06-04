// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

internal sealed class SimpleInvocationBinder : IInvocationBinder
{
    private readonly Type _returnType;

    public SimpleInvocationBinder(Type returnType)
    {
        _returnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
    }

    public Type GetReturnType(string invocationId)
    {
        return _returnType;
    }

    public IReadOnlyList<Type> GetParameterTypes(string methodName)
    {
        throw new NotImplementedException();
    }

    public Type GetStreamItemType(string streamId)
    {
        throw new NotImplementedException();
    }
}
