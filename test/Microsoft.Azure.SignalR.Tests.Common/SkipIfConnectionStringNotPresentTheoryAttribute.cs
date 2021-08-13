using Xunit;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public sealed class SkipIfConnectionStringNotPresentTheoryAttribute : TheoryAttribute
    {
        public SkipIfConnectionStringNotPresentTheoryAttribute()
        {
            if (!IsConnectionStringAvailable())
            {
                Skip = "Connection string is not available.";
            }
        }

        private static bool IsConnectionStringAvailable()
        {
            return !string.IsNullOrEmpty(TestConfiguration.Instance.ConnectionString);
        }
    }
}
