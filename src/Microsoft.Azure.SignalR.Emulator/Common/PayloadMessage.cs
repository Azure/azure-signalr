// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.Azure.SignalR.Controllers.Common
{
    internal class PayloadMessage
    {
        [Required]
        public string Target { get; set; }

        public object[] Arguments { get; set; }
    }
}
