// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace ChatSample;

public interface IChatHub
{
    Task BroadcastMessage(string name, string message);
    Task Echo(string name, string message);
}
