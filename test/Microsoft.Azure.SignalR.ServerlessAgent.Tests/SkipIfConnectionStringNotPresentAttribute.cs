using Microsoft.AspNetCore.Testing.xunit;
using System;

namespace Microsoft.Azure.SignalR.ServerlessAgent.Tests
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
