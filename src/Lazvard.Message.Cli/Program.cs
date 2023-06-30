using Lazvard.Message.Amqp.Server;
using Lazvard.Message.Amqp.Server.Helpers;
using Lazvard.Message.Cli;
using Microsoft.Extensions.Logging;


using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole();
});

Console.WriteLine("Lajvard ServiceBus Simulation");
Console.WriteLine("------------------------------");
Console.WriteLine("");

var (config, certificate) = await CliStartup.StartAsync();

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

Console.WriteLine($"Lajvard ServiceBus service is successfully listening at http://{config.IP}:{config.Port}");
Console.WriteLine ();
Console.Write($"ConnectionString: Endpoint=sb://{config.IP}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1");
Console.WriteLine();
Console.WriteLine();

await exitEvent.WaitAsync(default);
source.Cancel();
broker.Stop();

Console.WriteLine("Lajvard ServiceBus service successfully closed");
