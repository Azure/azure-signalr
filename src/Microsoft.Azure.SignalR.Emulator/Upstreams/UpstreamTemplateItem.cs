// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Emulator
{
    public class UpstreamOptions
    {
        public UpstreamTemplateItem[] Templates { get; set; }
    }

    public class UpstreamTemplateItem : IEquatable<UpstreamTemplateItem>
    {
        private static readonly HashSet<string> MatchAllPattern = new HashSet<string>(new[] { Constants.Asterisk });
        private static readonly HashSet<string> EmptyPattern = new HashSet<string>();

        private string _eventPattern = Constants.Asterisk;
        private string _hubPattern = Constants.Asterisk;
        private string _categoryPattern = Constants.Asterisk;

        private HashSet<string> _validEvents = MatchAllPattern;
        private HashSet<string> _validHubs = MatchAllPattern;
        private HashSet<string> _validCategories = MatchAllPattern;

        // 3 acceptable patterns:
        // 1. * to match all
        // 2. a,b,c to match multiple
        // 3. a to match single
        public string EventPattern
        {
            get => _eventPattern;
            set => SetPattern(value, ref _eventPattern, ref _validEvents);
        }

        public string HubPattern
        {
            get => _hubPattern;
            set => SetPattern(value, ref _hubPattern, ref _validHubs);
        }

        public string CategoryPattern
        {
            get => _categoryPattern;
            set => SetPattern(value, ref _categoryPattern, ref _validCategories);
        }

        public string UrlTemplate { get; set; }

        public bool IsMatch(InvokeUpstreamParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return IsMatch(parameters.Hub, _validHubs)
                   && IsMatch(parameters.Event, _validEvents)
                   && IsMatch(parameters.Category, _validCategories);
        }

        public bool Equals(UpstreamTemplateItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            return Equals(UrlTemplate, other.UrlTemplate)
                   && Equals(EventPattern, other.EventPattern)
                   && Equals(HubPattern, other.HubPattern)
                   && Equals(CategoryPattern, other.CategoryPattern);
        }

        public override bool Equals(object obj) => Equals(obj as UpstreamTemplateItem);

        public override int GetHashCode() => HashCode.Combine(UrlTemplate, EventPattern, HubPattern, CategoryPattern);

        private void SetPattern(string pattern, ref string field, ref HashSet<string> store)
        {
            if (field != pattern)
            {
                var events = pattern?.Split(',').Select(s => s.Trim());
                field = pattern;
                store = events == null ? EmptyPattern : new HashSet<string>(events);
            }
        }

        private static bool IsMatch(string input, HashSet<string> patterns)
        {
            // null or empty input can also be checked
            return patterns.Contains(Constants.Asterisk) || patterns.Contains(input);
        }
    }
}
