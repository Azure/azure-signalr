using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.E2ETest
{
    public static class Utils
    {
        public static string GetUniqueName(string prefix, int index)
        {
            return $"{prefix}.{index}";
        }
    }
}
