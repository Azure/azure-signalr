using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common.Auth
{
    internal interface IAadAuthOptions
    {
        Task<string> GenerateAadToken();
    }
}
