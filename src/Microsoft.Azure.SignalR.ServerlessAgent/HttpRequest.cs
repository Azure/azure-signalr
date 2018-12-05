using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class HttpRequest
    {
        private static readonly HttpClient _client = new HttpClient();
        HttpRequestMessage _request;

        public HttpRequest(string url, PayloadMessage payload, AccessToken accessToken, HttpMethod httpMethod)
        {
            _request = new HttpRequestMessage(httpMethod, url);
            _request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token.RawData);
            _request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        }

        public Task<HttpResponseMessage> SendAsync()
        {
            return _client.SendAsync(_request);
        }
    }
}
