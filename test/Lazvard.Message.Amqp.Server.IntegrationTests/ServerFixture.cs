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

    public ServerFixture(IMessageSink testOutputHelper)
    {
        source = new CancellationTokenSource();

        var config = new CliConfig()
        {
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
                    },
                }),
                new TopicConfig("Topic2", new []
                {
                    new TopicSubscriptionConfig("Subscription1"),
                    new TopicSubscriptionConfig("Subscription2")
                })
            ],
        };

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(new TestLoggerProvider(testOutputHelper));
        });

        var nodeFactory = new NodeFactory(loggerFactory, source.Token);
        var server = new Lazvard.Message.Cli.Server(nodeFactory, loggerFactory);
        broker = server.Start(config, null);
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
            var message = new DiagnosticMessage($"{DateTime.Now:ss.fff} {formatter(state, exception)}");
            testOutputHelper.OnMessage(message);
        }
    }
}