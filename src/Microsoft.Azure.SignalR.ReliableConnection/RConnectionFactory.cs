using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.SignalR
{
    public class RConnectionFactory : IConnectionFactory
    {
        private readonly RHttpConnectionFactory inner;

        public RConnectionFactory(RHttpConnectionFactory inner)
        {
            this.inner = inner;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var innerContext = await inner.ConnectAsync(endpoint, cancellationToken);
            return new RConnectionContext(innerContext, inner);
        }
    }

    public class RHttpConnectionFactory : IConnectionFactory
    {
        private readonly IServiceProvider _provider;
        private readonly ILoggerFactory _loggerFactory;

        public RHttpConnectionFactory(IServiceProvider provider, ILoggerFactory loggerFactory)
        {
            _provider = provider;
            _loggerFactory = loggerFactory;
        }
        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var options = _provider.GetRequiredService<IOptions<HttpConnectionOptions>>();
            options.Value.Url = ((UriEndPoint)endpoint).Uri;
            var inner = new HttpConnectionFactory(options, _loggerFactory);
            return inner.ConnectAsync(endpoint, cancellationToken);
        }
        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, string token, CancellationToken cancellationToken = default)
        {
            var options = _provider.GetRequiredService<IOptions<HttpConnectionOptions>>();
            options.Value.Url = ((UriEndPoint)endpoint).Uri;
            options.Value.AccessTokenProvider = () => Task.FromResult(token);
            var inner = new HttpConnectionFactory(options, _loggerFactory);
            return inner.ConnectAsync(endpoint, cancellationToken);
        }
    }
}
