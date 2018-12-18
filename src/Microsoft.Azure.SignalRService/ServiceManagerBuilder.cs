// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalRService
{
    public class ServiceManagerBuilder
    {
        public ServiceManagerBuilder WithCredentials(string connectionString)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithCredentials(string endpoint, string accessToken)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithCredentials(string endpoint, IList<string> accessTokens)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> options)
        {
            throw new NotImplementedException();
        }

        public ServiceManager Build()
        {
            throw new NotImplementedException();
        }
    }
}
