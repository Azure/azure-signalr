// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalRService
{
    public class ServiceManagerBuilder : IServiceManagerBuilder
    {
        public ServiceManagerBuilder WithCredentials(string connectionString)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> options)
        {
            throw new NotImplementedException();
        }

        public IServiceManager Build()
        {
            throw new NotImplementedException();
        }
    }
}
