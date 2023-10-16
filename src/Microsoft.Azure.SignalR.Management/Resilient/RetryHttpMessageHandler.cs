﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal class RetryHttpMessageHandler : DelegatingHandler
{
    private readonly IBackOffPolicy _retryDelayProvider;

    public RetryHttpMessageHandler(IBackOffPolicy retryDelayProvider)
    {
        _retryDelayProvider = retryDelayProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        IList<Exception>? exceptions = null;
        IEnumerator<TimeSpan>? delays = null;
        do
        {
            Exception? ex;
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (IsTransientError(response.StatusCode))
                {
                    var innerException = new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})");
                    ex = new AzureSignalRRuntimeException(response.RequestMessage?.RequestUri?.ToString(), innerException);
                    response.Dispose();
                }
                else
                {
                    return response;
                }
            }
            catch (OperationCanceledException operationCanceledException) when (!cancellationToken.IsCancellationRequested && operationCanceledException.InnerException is TimeoutException)
            {
                // Thrown by our timeout handler
                ex = operationCanceledException;
            }
            delays ??= _retryDelayProvider.GetDelays().GetEnumerator();
            if (!delays.MoveNext())
            {
                if (exceptions == null)
                {
                    throw ex;
                }
                else
                {
                    exceptions.Add(ex);
                    throw new AggregateException(exceptions);
                }
            }
            else
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
            await Task.Delay(delays.Current, cancellationToken);
        } while (true);
    }

    private static bool IsTransientError(HttpStatusCode code)
    {
        return code >= HttpStatusCode.InternalServerError ||
               code == HttpStatusCode.RequestTimeout;
    }
}
