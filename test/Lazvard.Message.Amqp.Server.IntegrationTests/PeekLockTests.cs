using Azure.Messaging.ServiceBus;

namespace Lazvard.Message.Amqp.Server.IntegrationTests
{
    [Collection(ServerCollection.Collection)]
    public class ReceiveAndDeleteTests : IClientFixture
    {
        private readonly ServiceBusClient client;

        public ReceiveAndDeleteTests(ClientFixture clientFixture)
        {
            this.client = clientFixture.Client;
        }

        [Fact]
        public async Task ReceiveAndDelete_CompleteRightAway_DonNotReceiveItAgain()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1", new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            await Task.Delay(1500);

            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1", new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
            var messages2 = await receiver2.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(300));
            Assert.Empty(messages2);
        }

        [Fact]
        public async Task ReceiveAndDelete_DeferMessage_ThrowInvalidOperationException()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1", new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => receiver1.DeferMessageAsync(messages1[0]));
        }
    }


    [Collection(ServerCollection.Collection)]
    public class PeekLockTests : IClientFixture
    {
        private readonly ServiceBusClient client;

        public PeekLockTests(ClientFixture clientFixture)
        {
            this.client = clientFixture.Client;
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_MessageNotCompleted_ReleaseAfterLockDuration()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver1.ReceiveMessagesAsync(1);

            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            //still locked fro receiver1
            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1");
            var messages2 = await receiver2.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(300));
            Assert.Empty(messages2);

            // wait for the message lock to be released
            var messages3 = await receiver2.ReceiveMessagesAsync(1);
            await receiver2.CompleteMessageAsync(messages3[0]);

            Assert.Single(messages3);
            Assert.Equal(messageBody, messages3[0].Body.ToString());
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_CompleteAfterLockDuration_ThrowServiceBusException()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            await Task.Delay(1500);

            await Assert.ThrowsAnyAsync<ServiceBusException>(() => receiver1.CompleteMessageAsync(messages1[0]));

            // maintain server state
            var messages2 = await receiver1.ReceiveMessagesAsync(1);
            await receiver1.CompleteMessageAsync(messages2[0]);
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_RenewLockAfterLockDuration_ThrowServiceBusException()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver.ReceiveMessagesAsync(1);
            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            await Task.Delay(1500);

            await Assert.ThrowsAnyAsync<ServiceBusException>(() => receiver.RenewMessageLockAsync(messages1[0]));

            // maintain server state
            var messages2 = await receiver.ReceiveMessagesAsync(1);
            await receiver.CompleteMessageAsync(messages2[0]);
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_RenewLock_KeepMessageLocke()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            //still locked fro receiver1
            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1");
            var messages2 = await receiver2.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(400));
            Assert.Empty(messages2);

            await receiver1.RenewMessageLockAsync(messages1[0]);

            var messages3 = await receiver2.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(400));
            Assert.Empty(messages2);

            await receiver1.CompleteMessageAsync(messages1[0]);
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_DeferMessage_DonNotReceiveItAgain()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);
            Assert.Equal(messageBody, messages1[0].Body.ToString());

            await receiver1.DeferMessageAsync(messages1[0]);

            // it will not delivered again
            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1");
            var messages2 = await receiver2.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(300));
            Assert.Empty(messages2);

            // unless use peek - maintain server state
            var deferredMessage = await receiver1.ReceiveDeferredMessageAsync(messages1[0].SequenceNumber);
            await receiver1.CompleteMessageAsync(deferredMessage);
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_DeadLetterMessage_MoveItToDeadLetter()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);

            await receiver1.DeadLetterMessageAsync(messages1[0]);

            await using var dlReceiver = client.CreateReceiver("Topic1", "Subscription1", new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });
            var messages2 = await dlReceiver.ReceiveMessagesAsync(1);
            Assert.Single(messages2);
            Assert.Equal(messageBody, messages2[0].Body.ToString());

            // maintain server state
            await dlReceiver.CompleteMessageAsync(messages2[0]);
        }

        [Fact]
        public async Task ReceiveMessagesInPeekLock_AbandonMessage_ReleaseTheMessage()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            var messages1 = await receiver1.ReceiveMessagesAsync(1);
            Assert.Single(messages1);

            await receiver1.AbandonMessageAsync(messages1[0]);

            // maintain server state
            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1");
            var messages2 = await receiver2.ReceiveMessagesAsync(1);
            Assert.Single(messages2);
            await receiver2.CompleteMessageAsync(messages2[0]);
        }
    }
}