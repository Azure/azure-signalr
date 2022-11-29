// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Controllers.Common
{
    public static class ErrorMessages
    {
        public static class Validation
        {
            public const string InvalidHubNameInPath = "Invalid hub name. Valid hub name should be in between 1 and 128 characters long. The first character must be a letter, other characters can be letters, digits or the following symbols _`,.[]";
            public const string InvalidApplicationName = "Invalid value of 'application'. Valid application name should be in between 1 and 128 characters long. The first character must be a letter, other characters can be letters, digits or the following symbols _`,.[]";
            public const string InvalidGroupName = "Invalid group name. Valid group name should be in between 1 and 1024 characters long.";
            public const string InvalidConnectionId = "Invalid connection id. Valid connection id should be more than 1 characters long";
            public const string MessageRequired = "Message in request body is required.";
        }
    }
}
