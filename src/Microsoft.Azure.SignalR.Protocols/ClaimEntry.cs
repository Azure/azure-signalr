// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public class ClaimEntry
    {
        [JsonProperty("t")]
        public string Type { get; set; }

        [JsonProperty("v")]
        public string Value { get; set; }

        public static ClaimEntry FromClaim(Claim claim)
        {
            return new ClaimEntry
            {
                Type = claim.Type,
                Value = claim.Value
            };
        }

        public Claim ToClaim()
        {
            return new Claim(Type, Value);
        }
    }
}
