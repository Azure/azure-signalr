using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class RestApis
    {
        private string _hubName;
        private string _endPoint;
        private RestApiVersions _version;

        public RestApis(string endpoint, string hubName, RestApiVersions version)
        {
            
            _endPoint = endpoint;
            _hubName = hubName;
            _version = version;
        }

        public string SendToUser(string userId)
        {
            return UrlPrefix() + AddHubName() + AddUserId(userId);
        }

        public string Broadcast()
        {
            return UrlPrefix() + AddHubName();
        }

        private string UrlPrefix()
        {
            return _endPoint + AddVersion();
        }

        private string AddVersion()
        {
            switch (_version)
            {
                case RestApiVersions.V1:
                    return "/api/v1";
                default:
                    return "/api/v1";
            }
        }

        private string AddHubName()
        {
            switch (_version)
            {
                case RestApiVersions.V1:
                    return "/hubs/" + _hubName;
                default:
                    return "/hubs/" + _hubName;
            }
        }

        private string AddUserId(string userId)
        {
            switch (_version)
            {
                case RestApiVersions.V1:
                    return "/users/" + userId;
                default:
                    return "/users/" + userId;
            }
        }
    }
}
