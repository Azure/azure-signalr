// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common
{
    [Serializable]
    public class AzureSignalRInaccessibleEndpointException : AzureSignalRException
    {
        private const string ErrorPhenomenon = "Unable to access SignalR service.";
        private const string SuggestAction = "Please make sure the endpoint or DNS setting is correct.";


        public AzureSignalRInaccessibleEndpointException(string requestUri, Exception innerException) : base(String.IsNullOrEmpty(requestUri) ? $"{ErrorPhenomenon} {innerException.Message} {SuggestAction}" : $"{ErrorPhenomenon} {innerException.Message} {SuggestAction} Request Uri: {requestUri}", innerException)
        {
        }

        protected AzureSignalRInaccessibleEndpointException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}