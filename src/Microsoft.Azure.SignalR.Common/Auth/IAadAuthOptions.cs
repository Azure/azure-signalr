using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IAadAuthOptions
    {
        Task<string> GenerateAadToken();
    }
}
