// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Microsoft.Azure.SignalR.Common
{
    [AttributeUsage(AttributeTargets.Assembly)]
    class ProductInfoAttribute : Attribute
    {
        public string PackageName { get; set; }
        public int Priority { get; set; }
    }
}
