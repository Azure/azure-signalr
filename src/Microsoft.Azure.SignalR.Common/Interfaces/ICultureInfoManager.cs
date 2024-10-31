// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    internal interface ICultureInfoManager
    {
        bool TryAddCulture(string clientRequestId, CultureInfo culture, CultureInfo uiCulture);

        bool TryApplyCulture(string clientRequestId);

        public void Cleanup();
    }
}
