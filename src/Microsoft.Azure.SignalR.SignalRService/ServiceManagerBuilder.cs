// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.SignalRService
{
    public class ServiceManagerBuilder
    {
        public ServiceManagerBuilder WithCredential(string conenctionString)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithCredential(string endpoint, string accessToken)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithCredential(string endpoint, IList<string> accessTokens)
        {
            throw new NotImplementedException();
        }

        public ServiceManagerBuilder WithOptions(Action<ServiceUtilityOptions> options)
        {
            throw new NotImplementedException();
        }

        public ServiceHubContext CreateHubContextAsync(string hubName)
        {
            throw new NotImplementedException();
        }

        public ServiceManager Build()
        {
            throw new NotImplementedException();
        }
    }
}
