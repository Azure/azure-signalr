// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    public class ServiceManagerBuilder : IServiceManagerBuilder
    {
        private readonly ServiceManagerOptions _options = new ServiceManagerOptions();

        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            configure?.Invoke(_options);
            return this;
        }

        public IServiceManager Build()
        {
            _options.ValidateOptions();
            return new ServiceManager(_options);
        }
    }
}