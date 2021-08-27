// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class SkipIfMultiEndpointsAbsentFactAttribute : FactAttribute
    {
        private static readonly string SkipReason = $"There are no multiple connection-string-based named endpoints under '{Constants.Keys.AzureSignalREndpointsKey}'.";

        public override string Skip { get => MultiEndpointsExist() ? null : SkipReason; set => base.Skip = value; }

        private static bool MultiEndpointsExist()
        {
            var config = TestConfiguration.Instance.Configuration;
            return config.GetEndpoints(Constants.Keys.AzureSignalREndpointsKey).Count() > 1;
        }
    }
}
