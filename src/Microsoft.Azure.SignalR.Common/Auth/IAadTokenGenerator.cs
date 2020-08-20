using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IAadTokenGenerator
    {
        Task<string> GenerateAccessToken();
    }
}
