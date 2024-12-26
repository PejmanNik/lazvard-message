using Iso8601DurationHelper;
using Lazvard.Message.Cli;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Lazvard.Message.Amqp.Server.IntegrationTests;

internal sealed class ServerFixture : IDisposable
{
    private readonly Broker broker;
    private readonly CancellationTokenSource source;
    public static CliConfig CliConfig = new()
    {
        MaxMessageSize = 64 * 1024 * 1024,
        Topics =
            [
                new TopicConfig("Queue1", new []
                {
                    new TopicSubscriptionConfig("")
                }),
                new TopicConfig("Topic1", new []
                {
                    new TopicSubscriptionConfig("Subscription1")
                    {
                        LockDuration = Duration.FromSeconds(1),
                        MaxDeliveryCount = 2,
                    },
                }),
                new TopicConfig("Topic2", new []
                {
                    new TopicSubscriptionConfig("Subscription1"),
                    new TopicSubscriptionConfig("Subscription2")
                })
            ],
    };

    public ServerFixture(IMessageSink testOutputHelper)
    {
        source = new CancellationTokenSource();

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(new TestLoggerProvider(testOutputHelper));
        });

        NodeFactory nodeFactory = new NodeFactory(loggerFactory, source.Token);
        Cli.Server server = new Lazvard.Message.Cli.Server(nodeFactory, loggerFactory);
        broker = server.Start(CliConfig, null);
    }

    public void Dispose()
    {
        source.Cancel();
        broker.Stop();
    }
}

[CollectionDefinition(Collection)]
public class ServerCollection : ICollectionFixture<ServerFixture>
{
    public const string Collection = "Server Collection";
}

public sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly IMessageSink testOutputHelper;

    public TestLoggerProvider(IMessageSink testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(testOutputHelper);
    }

    public void Dispose()
    {
    }

    public class Logger : ILogger
    {
        private readonly IMessageSink testOutputHelper;

        public Logger(IMessageSink testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            DiagnosticMessage message = new DiagnosticMessage($"{DateTime.Now:ss.fff} {formatter(state, exception)}");
            testOutputHelper.OnMessage(message);
        }
    }
}