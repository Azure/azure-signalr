// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    public static class HttpResponseMessageExtensions
    {
        public static void CheckResponse(this HttpResponseMessage response, InvokeUpstreamParameters parameters, ILogger logger, Func<HttpResponseMessage, bool> expectedEvaluator)
        {
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                // already logged inside
                return;
            }

            var operationName = parameters.ToString();

            if (expectedEvaluator.Invoke(response))
            {
                logger.LogInformation($"Upstream successfully sent message for {operationName}.");
            }
            else
            {
                logger.LogError($"The response for {operationName} is unexpected: {response.StatusCode}.");
            }
        }
    }
}
