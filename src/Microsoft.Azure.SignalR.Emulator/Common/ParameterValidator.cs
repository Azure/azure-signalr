// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Microsoft.Azure.SignalR.Controllers.Common
{
    public class ParameterValidator
    {
        public const string HubNamePattern = "^[A-Za-z][A-Za-z0-9_`,.[\\]]{0,127}$";
        public const string NotWhitespacePattern = "^(?!\\s+$).+$";
    }
}
