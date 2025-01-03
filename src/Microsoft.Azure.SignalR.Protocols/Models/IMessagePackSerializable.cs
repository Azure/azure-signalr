// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;

namespace Microsoft.Azure.SignalR.Protocol;
internal interface IMessagePackSerializable
{
    void Serialize(ref MessagePackWriter writer);

    //.NET Standard 2.0 dones't support static abstract members.
    //As a workaround, we have to new an instance, and then load members from reader.
    void Load(ref MessagePackReader reader, string fieldName);
}

internal static class IMessagePackSerializableExtensions
{
    public static TSelf Deserialize<TSelf>(ref this MessagePackReader reader, string fieldName) where TSelf : IMessagePackSerializable, new()
    {
        var result = new TSelf();
        result.Load(ref reader, fieldName);
        return result;
    }
}
