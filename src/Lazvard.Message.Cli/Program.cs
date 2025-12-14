using Lazvard.Message.Cli;
using Microsoft.Extensions.Logging;
using Spectre.Console;


using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole();
});

AnsiConsole.Write(
    new FigletText("Lajvard")
    .Color(Color.Blue3_1)
    );
AnsiConsole.WriteLine("");
AnsiConsole.WriteLine("  ServiceBus Simulation");
AnsiConsole.Write(new Rule());
AnsiConsole.WriteLine("");


return await CommandHandler.Handle(args, loggerFactory);