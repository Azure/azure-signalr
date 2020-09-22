// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Threading;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceStatusMonitor : IDisposable
    {
        private readonly IServiceManager _serviceManager;
        private readonly ServiceEndpoint _endpoint;
        private readonly Timer Timer;
        private CancellationTokenSource TokenSource;
        private bool _disposedValue;

        public ServiceStatusMonitor(IServiceManager serviceManager, ServiceEndpoint endpoint)
        {
            _serviceManager = serviceManager;
            _endpoint = endpoint;
            Timer = new Timer(CheckStatus, null, 0, Timeout.Infinite);
        }

        private async void CheckStatus(object _)
        {
            using var tokenSource = new CancellationTokenSource();
            TokenSource = tokenSource;
            var isHealthy = await _serviceManager.IsServiceHealthy(TokenSource.Token);
            if (isHealthy)
            {
                _endpoint.Online = true;
            }
            else
            {
                _endpoint.Online = false;
            }
            Timer.Change(TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Timer?.Dispose();
                    TokenSource?.Cancel();
                    TokenSource?.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}