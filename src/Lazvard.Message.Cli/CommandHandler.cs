using Lazvard.Message.Amqp.Server;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.CommandLine;

namespace Lazvard.Message.Cli;

public static class CommandHandler
{
    public static async Task Handle(string[] args, ILoggerFactory loggerFactory)
    {
        var rootCommand = new RootCommand("Lazvard Message Command Line");

        var configOption = new Option<string?>(
            name: "--config",
            description: "The TOML config file path");

        configOption.AddAlias("-c");
        configOption.IsRequired = false;

        rootCommand.AddOption(configOption);

        rootCommand.SetHandler((configPath) =>
        {
            return RunServer(configPath, loggerFactory);
        }, configOption);

        await rootCommand.InvokeAsync(args);
    }

    public static async Task RunServer(string? configPath, ILoggerFactory loggerFactory)
    {
        var (config, certificate) = await AMQPServerHandler
            .StartAsync(new AMQPServerParameters(configPath));

        Console.WriteLine($"Lajvard ServiceBus service is successfully listening at http://{config.IP}:{config.Port}");
        Console.WriteLine();

        var connectionStringPanel = new Panel($"ConnectionString: Endpoint=sb://{config.IP}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1");
        connectionStringPanel.Border = BoxBorder.Double;
        connectionStringPanel.Padding = new Padding(1, 1, 1, 1);
        AnsiConsole.Write(connectionStringPanel);
        AnsiConsole.WriteLine();

        var source = new CancellationTokenSource();
        var exitEvent = new AsyncManualResetEvent(false);
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Lajvard ServiceBus service is closing");

            eventArgs.Cancel = true;
            exitEvent.Set();
        };

        var nodeFactory = new NodeFactory(loggerFactory, source.Token);
        var server = new Server(nodeFactory, loggerFactory);
        var broker = server.Start(config, certificate);

        Console.WriteLine();

        await exitEvent.WaitAsync(default);
        source.Cancel();
        broker.Stop();

        Console.WriteLine("Lajvard ServiceBus service successfully closed");
    }
}
