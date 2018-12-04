using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class AgentContext
    {
        public string HubName { get; set; }
        public Backend Backend { get; set; }
        public RestApiVersions RestApiVersion { get; set; }
        public Credential Credentail { get; set; } = new Credential();
        public string Endpoint { get; set; }

        public string GetEndpoint()
        {
            if (Credentail.SignalrServiceCredential != null)
            {
                return Credentail.SignalrServiceCredential.Endpoint;
            }
            
            if (Endpoint != null)
            {
                return Endpoint;
            }

            throw new Exception("Cannot find audience");
        }
    }
}
