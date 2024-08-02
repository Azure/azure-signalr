using System;
using System.Threading.Tasks;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class AckHandlerTest
{
    [Fact]
    public void TestOnce()
    {
        var handler = new AckHandler();
        var task = handler.CreateSingleAck(out var ackId);
        handler.TriggerAck(ackId);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(AckStatus.Ok, task.Result);
    }

    [Fact]
    public async Task TestOnce_Timeout()
    {
        var handler = new AckHandler(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(20));
        var task = handler.CreateSingleAck(out var ackId);
        Assert.False(task.IsCompleted);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        Assert.True(task.IsCompleted);
        // This assertion is different from RT for different behaviour when timeout of AckHandler. See annotation in AckHandler.cs method CheckAcs
        Assert.Equal(AckStatus.Timeout, task.Result);
    }

    [Fact]
    public void TestTwice_SetExpectedFirst()
    {
        var handler = new AckHandler();
        var task = handler.CreateMultiAck(out var ackId);
        handler.SetExpectedCount(ackId, 2);
        handler.TriggerAck(ackId);
        Assert.False(task.IsCompleted);
        handler.TriggerAck(ackId);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void TestTwice_AckFirst()
    {
        var handler = new AckHandler();
        var task = handler.CreateMultiAck(out var ackId);
        handler.TriggerAck(ackId);
        Assert.False(task.IsCompleted);
        handler.TriggerAck(ackId);
        Assert.False(task.IsCompleted);
        handler.SetExpectedCount(ackId, 2);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task TestTwice_Timeout()
    {
        var handler = new AckHandler(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(20));
        var task = handler.CreateMultiAck(out var ackId);
        Assert.False(task.IsCompleted);
        handler.SetExpectedCount(ackId, 2);
        Assert.False(task.IsCompleted);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        Assert.True(task.IsCompleted);
        // This assertion is different from RT for different behaviour when timeout of AckHandler. See annotation in AckHandler.cs method CheckAcs
        Assert.Equal(AckStatus.Timeout, task.Result);
    }

    [Fact]
    public void TestInvalid_SetExpectedForSingle()
    {
        var handler = new AckHandler(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(20));
        var task = handler.CreateSingleAck(out var ackId);
        Assert.Throws<InvalidOperationException>(() => handler.SetExpectedCount(ackId, 2));
    }
}
