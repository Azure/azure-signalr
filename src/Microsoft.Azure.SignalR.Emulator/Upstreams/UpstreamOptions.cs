// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal class UpstreamOptions
    {
        public UpstreamTemplateItem[] Templates { get; set; }

        public override string ToString()
        {
            if (Templates?.Length > 0)
            {
                var sb = new StringBuilder();
                for (var i = 0; i < Templates.Length; i++)
                {
                    sb.AppendLine($"\t[{i}]{Templates[i]}");
                }
                return sb.ToString();
            }

            return "No upstream is set yet. Use 'upstream init' to create default upstreams.";
        }
    }
}
