using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common.Auth
{
    internal interface ITokenBasedAuthOptions
    {
        Task<string> AcquireAccessToken();
    }
}
