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

        var initConfigOption = new Option<bool>(
            name: "--init-config",
            description: "Create config file");
        initConfigOption.AddAlias("-ic");
        initConfigOption.IsRequired = false;
        initConfigOption.SetDefaultValue(true);

        var silentOption = new Option<bool>(
            name: "--silent",
            description: "Suppress all user input prompt");
        silentOption.AddAlias("-s");
        silentOption.IsRequired = false;

        var configOption = new Option<string?>(
            name: "--config",
            description: "The TOML config file path");

        configOption.AddAlias("-c");
        configOption.IsRequired = false;

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(silentOption);
        rootCommand.AddOption(initConfigOption);

        rootCommand.SetHandler((configPath, isSilent, initConfig) =>
        {
            return RunServer(new AMQPServerParameters(configPath, isSilent, initConfig), loggerFactory);
        }, configOption, silentOption, initConfigOption);

        await rootCommand.InvokeAsync(args);
    }

    public static async Task RunServer(AMQPServerParameters parameters, ILoggerFactory loggerFactory)
    {
        var (config, certificate) = await AMQPServerHandler.StartAsync(parameters);

        Console.WriteLine($"Lajvard ServiceBus service is successfully listening at http://{config.IP}:{config.Port}");
        Console.WriteLine();

        var connectionStringPanel = new Panel($"ConnectionString: Endpoint=sb://{config.IP}{(!config.UseHttps ? $":{config.Port}" : string.Empty)}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=true;")
        {
            Border = BoxBorder.Double,
            Padding = new Padding(1, 1, 1, 1)
        };
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
