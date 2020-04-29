// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsValidation : IValidateOptions<ServiceOptions>
    {
        public ValidateOptionsResult Validate(string name, ServiceOptions options)
        {
            if (options.ConnectionCount < 0)
            {
                return ValidateOptionsResult.Fail("ConnectionCount should be positive integer.");
            }

            if (options.DisconnectTimeoutInSeconds.HasValue && 
                (options.DisconnectTimeoutInSeconds < 1 || options.DisconnectTimeoutInSeconds > 300))
            {
                return ValidateOptionsResult.Fail("DisconnectTimeoutInSeconds is out of range [1,300].");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
