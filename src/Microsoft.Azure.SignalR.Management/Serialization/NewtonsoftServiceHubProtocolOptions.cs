﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.SignalR.Management
{
    public class NewtonsoftServiceHubProtocolOptions
    {
        /// <summary>
        /// Gets or sets the settings used to serialize invocation arguments and return values.
        /// </summary>
        public JsonSerializerSettings PayloadSerializerSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }
}