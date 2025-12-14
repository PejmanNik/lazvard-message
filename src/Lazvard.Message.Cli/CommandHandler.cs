using Lazvard.Message.Amqp.Server;
using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.CommandLine;

namespace Lazvard.Message.Cli;

public static class CommandHandler
{
    public static Task<int> Handle(string[] args, ILoggerFactory loggerFactory)
    {
        var rootCommand = new RootCommand("Lazvard Message Command Line");

        var initConfigOption = new Option<bool>("--init-config", "-ic")
        {
            Description = "Create config file", Required = true, DefaultValueFactory = (_) => true
        };

        var silentOption = new Option<bool>("--silent", "-s")
        {
            Description = "Suppress all user input prompt", Required = false,
        };

        var configOption = new Option<string?>("--config", "-c")
        {
            Description = "The TOML config file path", Required = false,
        };

        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(silentOption);
        rootCommand.Options.Add(initConfigOption);

        rootCommand.SetAction((parsed) => RunServer(new AMQPServerParameters(
                parsed.GetValue(configOption),
                parsed.GetValue(silentOption),
                parsed.GetValue(initConfigOption)),
            loggerFactory));

        var parseResult = rootCommand.Parse(args);
        return parseResult.InvokeAsync();
    }

    public static async Task RunServer(AMQPServerParameters parameters, ILoggerFactory loggerFactory)
    {
        var (config, certificate) = await AMQPServerHandler.StartAsync(parameters);

        Console.WriteLine($"Lajvard ServiceBus service is successfully listening at http://{config.IP}:{config.Port}");
        Console.WriteLine();

        var connectionStringPanel =
            new Panel(
                $"ConnectionString: Endpoint=sb://{config.IP}{(!config.UseHttps ? $":{config.Port}" : string.Empty)}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=true;")
            {
                Border = BoxBorder.Double, Padding = new Padding(1, 1, 1, 1)
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