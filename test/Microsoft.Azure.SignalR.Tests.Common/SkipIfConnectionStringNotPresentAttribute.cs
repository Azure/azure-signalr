﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Testing.xunit;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class SkipIfConnectionStringNotPresentAttribute : Attribute, ITestCondition
    {
        public bool IsMet => IsConnectionStringAvailable();

        public string SkipReason => "Connection string is not available.";

        private static bool IsConnectionStringAvailable()
        {
            return !string.IsNullOrEmpty(TestConfiguration.Instance.ConnectionString);
        }
    }
}

