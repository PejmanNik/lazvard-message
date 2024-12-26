using Azure.Messaging.ServiceBus;

namespace Lazvard.Message.Amqp.Server.IntegrationTests
{
    [Collection(ServerCollection.Collection)]
    public class DeadLetterTests : IClientFixture
    {
        private readonly ServiceBusClient client;
        private readonly int maxDeliveryCount;

        public DeadLetterTests(ClientFixture clientFixture)
        {
            client = clientFixture.Client;
            maxDeliveryCount = ServerFixture.CliConfig.Topics
               .Where(x => x.Name == "Topic1")
               .Single()
               .Subscriptions
               .Single()
               .MaxDeliveryCount;
        }

        [Fact]
        public async Task MessageNotCompletedForMaxDeliveryCount_MoveMessageToDeadLetter()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            for (var i = 0; i < maxDeliveryCount; i++)
            {
                var messages1 = await receiver1.ReceiveMessagesAsync(1);
                Assert.Single(messages1);
                await receiver1.AbandonMessageAsync(messages1[0]);
            }

            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1", new()
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
            var messages2 = await receiver2.ReceiveMessagesAsync(1);
            Assert.Single(messages2);
            Assert.Equal(messageBody, messages2[0].Body.ToString());
        }
        [Fact]
        public async Task AbandonTheMessageForMaxDeliveryCount_MoveMessageToDeadLetter()
        {
            var messageBody = "Test message 1";
            await using var sender = client.CreateSender("Topic1");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic1", "Subscription1");
            for (var i = 0; i < maxDeliveryCount; i++)
            {
                var messages1 = await receiver1.ReceiveMessagesAsync(1);
                Assert.Single(messages1);
                await Task.Delay(1000);
            }

            await using var receiver2 = client.CreateReceiver("Topic1", "Subscription1", new()
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
            var messages2 = await receiver2.ReceiveMessagesAsync(1);
            Assert.Single(messages2);
            Assert.Equal(messageBody, messages2[0].Body.ToString());
        }
    }
}
