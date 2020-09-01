// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.SignalR.Emulator
{
    public static class Utils
    {
        private static readonly Regex UpstreamReplaceRegex = new Regex("\\{(?:hub|category|event)\\}", RegexOptions.Compiled);

        public static IEnumerable<string> GetConnectionSignature(string connectionId, IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                yield break;
            }

            foreach (var key in keys)
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(connectionId));
                    yield return "sha256=" + BitConverter.ToString(hashBytes).Replace("-", ""); // Maybe there's a more efficient way
                }
            }
        }

        internal static Uri GetUpstreamUrl(string template, string hub, string category, string @event)
        {
            if (string.IsNullOrEmpty(template))
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (string.IsNullOrEmpty(hub))
            {
                throw new ArgumentNullException(nameof(hub));
            }

            if (string.IsNullOrEmpty(category))
            {
                throw new ArgumentNullException(nameof(category));
            }

            if (string.IsNullOrEmpty(@event))
            {
                throw new ArgumentNullException(nameof(@event));
            }

            var replaced = UpstreamReplaceRegex.Replace(template, m =>
            {
                switch (m.Value)
                {
                    case "{hub}":
                        return Uri.EscapeDataString(hub);
                    case "{category}":
                        return Uri.EscapeDataString(category);
                    case "{event}":
                        return Uri.EscapeDataString(@event);
                    default:
                        throw new InvalidOperationException($"Invalid template {m.Value}");
                }
            });

            if (!Uri.TryCreate(replaced, UriKind.Absolute, out var result))
            {
                throw new ArgumentException($"The Upstream url {replaced} is not in a validate absolute URI format.");
            }

            return result;
        }
    }
}
