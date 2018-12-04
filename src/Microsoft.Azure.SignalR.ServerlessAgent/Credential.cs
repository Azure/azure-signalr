using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class Credential
    {
        public SignalRServiceCredential SignalrServiceCredential;
        public IList<AccessToken> AccessTokens { get; } = new List<AccessToken>();

        public void AddAccessToken(string token)
        {
            AccessTokens.Add(new AccessToken(token));
        }

        public bool TryGetFirstTokenForAudience(string audience, out AccessToken tokenOut)
        {
            if (SignalrServiceCredential != null)
            {
                tokenOut = new AccessToken(AccessTokenGenerator.GenerateAccessToken(audience, SignalrServiceCredential.AccessKey, TimeSpan.FromHours(1)));
                return true;
            }

            foreach (var accessToken in AccessTokens)
            {
                var audiencesInToken = accessToken.Token.Audiences;
                foreach (var audienceInToken in audiencesInToken)
                {
                    if (audience == audienceInToken)
                    {
                        tokenOut = accessToken;
                        return true;
                    }
                }
            }

            tokenOut = null;
            return false;
        }
    }
}
