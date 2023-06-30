using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;

public interface ISubscription
{
    string Name { get; }

    bool HasAddress(Address address);
    void OnAttachSendingLink(SendingAmqpLink link);
    void Write(AmqpMessage message);
}

public abstract class SubscriptionBase : ISubscription
{
    private readonly CancellationToken stopToken;
    private readonly AsyncAutoResetEvent emptyConsumerEvent;
    private readonly ConsumerFactory consumerFactory;

    protected readonly ConcurrentDictionary<string, Consumer> consumers;
    protected readonly IMessageQueue messageQueue;
    protected readonly TopicSubscriptionConfig config;
    protected readonly ILogger logger;

    public string Name => config.Name;

    public SubscriptionBase(
        TopicSubscriptionConfig config,
        IMessageQueue messageQueue,
        ConsumerFactory consumerFactory,
        ILoggerFactory loggerFactory,
        CancellationToken stopToken)
    {
        this.config = config;
        this.stopToken = stopToken;
        this.messageQueue = messageQueue;
        this.consumerFactory = consumerFactory;

        logger = loggerFactory.CreateLogger<Subscription>();
        consumers = new(2, 5);
        emptyConsumerEvent = new();

        _ = Task.Run(ProcessIncomingMessages, stopToken);
    }

    public bool HasAddress(Address address)
    {
        return consumers.ContainsKey(address.ToString() ?? "");
    }

    public void Write(AmqpMessage message)
    {
        var sequenceNumber = messageQueue.Enqueue(message);
        logger.LogTrace("write message {MessageSeqNo} to subscription {Subscription} channel", sequenceNumber, Name);
    }

    public void OnAttachSendingLink(SendingAmqpLink link)
    {
        var id = ((Target?)link.Settings.Target)?.Address?.ToString() ?? link.Name;
        if (id is null)
        {
            link.SafeClose(new AmqpException(AmqpErrorCode.InternalError, "The link address is not valid"));
            return;
        }

        if (consumers.TryAdd(id, consumerFactory.Create(link, messageQueue, emptyConsumerEvent.Set)))
        {
            link.Closed += new EventHandler((s, e) =>
            {
                logger.LogTrace("close link {Link}", link.Name);
                consumers.TryRemove(id, out _);
            });
        }
    }


    private async Task ProcessIncomingMessages()
    {
        // limit the parallel message processing as this is a simulator
        // and we only need to deliver messages unordered
        var maxProcessThreads = 3;
        var semaphoreSlim = new SemaphoreSlim(maxProcessThreads, maxProcessThreads);

        while (!stopToken.IsCancellationRequested)
        {
            try
            {
                logger.LogTrace("waiting to receive a message in subscription {Subscription}",
                    Name);

                // wait for receiving a message
                var message = await messageQueue.DequeueAsync(stopToken);
                if (!message.IsSuccess)
                {
                    continue;
                }

                var activeConsumers = consumers.Values.Where(x => !x.IsDrain);

                if (!activeConsumers.Any())
                {
                    logger.LogTrace("no consumers to deliver the message {MessageSeqNo} in subscription {Subscription}, waiting...",
                        message.Value.GetTraceId(), Name);

                    // wait for a active consumer
                    await emptyConsumerEvent.WaitAsync(stopToken);
                }

                await semaphoreSlim.WaitAsync();
                _ = Task.Run(
                    () => ProcessIncomingMessage(message.Value, stopToken), stopToken)
                    .ContinueWith(t => semaphoreSlim.Release()
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Process incoming messages failed with exception");
            }
        }
    }

    protected abstract void ProcessIncomingMessage(AmqpMessage message, CancellationToken stopToken);
}
