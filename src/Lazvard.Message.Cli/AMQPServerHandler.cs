using Spectre.Console;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public class AMQPServerParameters(string? configPath, bool isSilent, bool initConfigFile)
{
    public string? ConfigPath { get; } = configPath;
    public bool IsSilent { get; } = isSilent;
    public bool InitConfigFile { get; } = initConfigFile;
}

public class AMQPServerHandler
{
    public static async Task<(CliConfig, X509Certificate2?)> StartAsync(AMQPServerParameters parameters)
    {
        var (configPath, configExists) = Configuration.GetConfigPath(parameters.ConfigPath);
        if (!configExists && parameters.InitConfigFile)
        {
            AnsiConsole.WriteLine("Can't find the config file, creating the init config file...");
            await Configuration.WriteAsync(Configuration.CreateDefaultConfig(), configPath);
            AnsiConsole.WriteLine("default config file successfully created!");

            AnsiConsole.WriteLine();
        }
        else if (!configExists)
        {
            AnsiConsole.MarkupLine($"[red]Can't find the config file in '{parameters.ConfigPath}'[/]");
            AnsiConsole.WriteLine();
            Environment.Exit(0);
        }

        var config = Configuration.Read(configPath);
        if (!config.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Can't load the config file, Please check the syntax[/]");
            AnsiConsole.MarkupLine($"[red]Error: {config.Error}[/]");
            Environment.Exit(0);
        }

        if (config.Value.UseHttps)
        {
            if (string.IsNullOrEmpty(config.Value.CertificatePath))
            {
                AnsiConsole.MarkupLine($"[red]Certificate path is empty, please check the config file, or turn of UseHttps option[/]");
                Environment.Exit(0);
            }
            var certificateResult = CertificateHandler.ReadCertificateFromFile(
                config.Value.CertificatePath,
                config.Value.CertificatePassword);

            if (!certificateResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]Can't load the certificate. please check the certificate[/]");
                AnsiConsole.MarkupLine($"[red]Error: {certificateResult.Error}[/]");

                Environment.Exit(0);
            }

            return (config.Value, certificateResult.Value);
        }

        return (config.Value, null);
    }
}
