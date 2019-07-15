using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class ParameterDelegator
    {
        public const string ApplicationName = "ApplicationName";

        public Dictionary<string, object> Parameter { get; } = new Dictionary<string, object>();

        public ParameterDelegator ConfigApplicationName(string applicationName)
        {
            Parameter[ApplicationName] = applicationName;
            return this;
        }
    }
}
