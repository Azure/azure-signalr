// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;

namespace Microsoft.Azure.SignalR.Emulator
{
    public class InvokeUpstreamParameters
    {
        public InvokeUpstreamParameters(string hub, string category, string @event)
        {
            Hub = hub;
            Category = category;
            Event = @event;
        }

        public string Hub { get; }
        public string Category { get; }
        public string Event { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{hub}=");
            sb.Append(Hub);
            sb.Append(",");
            sb.Append("{event}=");
            sb.Append(Event);
            sb.Append(",");
            sb.Append("{category}=");
            sb.Append(Category);
            return sb.ToString();
        }
    }
}
