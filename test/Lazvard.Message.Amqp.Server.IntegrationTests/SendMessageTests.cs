using Azure.Messaging.ServiceBus;
using System.Text;

namespace Lazvard.Message.Amqp.Server.IntegrationTests
{
    [Collection(ServerCollection.Collection)]
    public class SendMessageTests : IClientFixture
    {
        private readonly ServiceBusClient client;

        public SendMessageTests(ClientFixture clientFixture)
        {
            this.client = clientFixture.Client;
        }

        [Fact]
        public async Task SendAndReceiveMessages_Topic_EachSubscriptionGetTheMessages()
        {
            var messageBody = "Test message 1";

            await using var sender = client.CreateSender("Topic2");
            await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

            await using var receiver1 = client.CreateReceiver("Topic2", "Subscription1");
            await using var receiver2 = client.CreateReceiver("Topic2", "Subscription2");

            var messagesTask1 = receiver1.ReceiveMessagesAsync(1);
            var messagesTask2 = receiver2.ReceiveMessagesAsync(1);

            var results = await Task.WhenAll(messagesTask1, messagesTask2);

            Assert.Single(results[0]);
            Assert.Single(results[1]);
            Assert.Equal(messageBody, results[0][0].Body.ToString());
            Assert.Equal(messageBody, results[1][0].Body.ToString());

            await receiver1.CompleteMessageAsync(results[0][0]);
            await receiver2.CompleteMessageAsync(results[1][0]);
        }

        [Fact]
        public async Task SendAndReceiveMessages_Queue_GetTheMessages()
        {
            var messageBody1 = "Test message 1";
            var messageBody2 = Encoding.UTF8.GetBytes("تست 测试 message 2");

            var message1 = new ServiceBusMessage(messageBody1);
            var message2 = new ServiceBusMessage(messageBody2);

            message1.MessageId = "MID1";
            message1.ApplicationProperties.Add("TestAP", 1.34);

            await using var sender = client.CreateSender("Queue1");
            await sender.SendMessageAsync(message1);
            await sender.SendMessageAsync(message2);

            await using var receiver = client.CreateReceiver("Queue1");

            var messages = await receiver.ReceiveMessagesAsync(2);

            Assert.Equal(2, messages.Count);

            Assert.True(messages[0].SequenceNumber > 0);
            Assert.True(messages[1].SequenceNumber > 0);
            Assert.True(messages[0].SequenceNumber != messages[1].SequenceNumber);

            Assert.Contains(messages, x => x.Body.ToString() == messageBody1);
            Assert.Contains(messages, x => x.ApplicationProperties.TryGetValue("TestAP", out var ap) && (double)ap == 1.34);
            Assert.Contains(messages, x => x.MessageId == "MID1");

            Assert.Contains(messages, x => x.Body.ToArray().SequenceEqual(messageBody2));

            await receiver.CompleteMessageAsync(messages[0]);
        }
    }
}