using Azure.Messaging.ServiceBus;

namespace Lazvard.Message.Amqp.Server.IntegrationTests;

[Collection(ServerCollection.Collection)]
public class DeferMessageTests : IClientFixture
{
    private readonly ServiceBusClient client;

    public DeferMessageTests(ClientFixture clientFixture)
    {
        this.client = clientFixture.Client;
    }

    private async Task<long> DeferMessageAsync(ServiceBusReceiver receiver1)
    {
        var messageBody = "Test message 1";
        await using var sender = client.CreateSender("Topic1");
        await sender.SendMessageAsync(new ServiceBusMessage(messageBody));


        var messages1 = await receiver1.ReceiveMessagesAsync(1);
        Assert.Single(messages1);

        await receiver1.DeferMessageAsync(messages1[0]);
        return messages1[0].SequenceNumber;
    }

    [Fact]
    public async Task DeferMessage_PeekLockAndDispositionWithCompleting_ReturnOK()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
        var messageSequenceNumber = await DeferMessageAsync(receiver1);


        var deferredMessage = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.CompleteMessageAsync(deferredMessage);
    }

    [Fact]
    public async Task DeferMessage_ReceiveAndDelete_ReturnOK()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1", new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var messageSequenceNumber = await DeferMessageAsync(receiver1);

        await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1", new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        var deferredMessage = await receiver2.ReceiveDeferredMessageAsync(messageSequenceNumber);
        Assert.NotNull(deferredMessage);

        //can not find the message
        await Assert.ThrowsAnyAsync<ServiceBusException>(() => receiver2.ReceiveDeferredMessageAsync(messageSequenceNumber));
    }

    [Fact]
    public async Task DeferMessage_DispositionWithDefer_ReturnOK()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
        var messageSequenceNumber = await DeferMessageAsync(receiver1);


        var deferredMessage = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.DeferMessageAsync(deferredMessage);


        // maintain server state
        var deferredMessage2 = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.CompleteMessageAsync(deferredMessage2);
    }

    [Fact]
    public async Task DeferMessage_DispositionWithAbandon_ReturnOK()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
        var messageSequenceNumber = await DeferMessageAsync(receiver1);


        var deferredMessage = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.AbandonMessageAsync(deferredMessage);


        // maintain server state
        var deferredMessage2 = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.CompleteMessageAsync(deferredMessage2);
    }

    [Fact]
    public async Task DeferMessage_SendToDeadLetter_ReturnOK()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
        var messageSequenceNumber = await DeferMessageAsync(receiver1);

        var deferredMessage = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.DeadLetterMessageAsync(deferredMessage);

        await Assert.ThrowsAnyAsync<ServiceBusException>(() => receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber));
    }

    [Fact]
    public async Task DeferMessage_PeekMessages_ReceiveTheMessage()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
        var messageSequenceNumber = await DeferMessageAsync(receiver1);

        var peekMessages = await receiver1.PeekMessageAsync(0);
        Assert.NotNull(peekMessages);

        // maintain server state
        var deferredMessage2 = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);
        await receiver1.CompleteMessageAsync(deferredMessage2);
    }

    [Fact]
    public async Task DeferMessage_RenewLock_KeepTheLock()
    {
        await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
        var messageSequenceNumber = await DeferMessageAsync(receiver1);

        var deferredMessage = await receiver1.ReceiveDeferredMessageAsync(messageSequenceNumber);

        await Task.Delay(400);
        await receiver1.RenewMessageLockAsync(deferredMessage);
        await Task.Delay(200);

        // still has the lock    
        await receiver1.CompleteMessageAsync(deferredMessage);
    }
}
