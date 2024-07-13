using Lazvard.Message.Amqp.Server.Helpers;
using Spectre.Console;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public class AMQPServerParameters(string? configPath)
{
    public string? ConfigPath { get; } = configPath;
}

public class AMQPServerHandler
{
    public static async Task<(CliConfig, X509Certificate2?)> StartAsync(AMQPServerParameters parameters)
    {
        var (configPath, configExists) = Configuration.GetConfigPath(parameters.ConfigPath);
        if (!configExists && !string.IsNullOrEmpty(parameters.ConfigPath))
        {
            AnsiConsole.MarkupLine($"[red]Can't find the config file in '{parameters.ConfigPath}'[/]");
            AnsiConsole.WriteLine();
            Environment.Exit(0);
        }
        if (!configExists)
        {
            AnsiConsole.WriteLine("Can't find the config file, creating the init config file...");
            AnsiConsole.WriteLine();


            await Configuration.CreateDefaultConfigAsync();
            AnsiConsole.WriteLine("default config file successfully created!");

            AnsiConsole.WriteLine("The latest version of the Azure SDK no longer requires an HTTPS connection for the local server.");
            if (AnsiConsole.Confirm("Do you want to use Https?", false))
            {
                var certificateStoreResult = CertificateHandler.ReadCertificateFromStore();
                if (!certificateStoreResult.IsSuccess)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine("ServiceBus needs a valid certificate to start HTTPs server.");
                    AnsiConsole.WriteLine("You can add the certificate info in the config file,");
                    AnsiConsole.WriteLine("or create a new certificate using built-in certificate manager (powered by dotnet dev-certs)");
                    AnsiConsole.WriteLine();

                    var addCertificateResult = AddCertificate();
                    if (!addCertificateResult.IsSuccess)
                    {
                        Environment.Exit(0);
                    }
                }
            }
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
            var cert = ReadCertificate(
                config.Value.UseBuiltInCertificateManager,
                config.Value.CertificatePath,
                config.Value.CertificatePassword);

            if (!cert.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]Can't load the certificate. please check the certificate or use the built-in certificate manager[/]");
                AnsiConsole.MarkupLine($"[red]In order to troubleshoot the built-in certificate manager please use dotnet dev-certs documentation[/]");
                AnsiConsole.MarkupLine($"[red]Error: {cert.Error}[/]");

                Environment.Exit(0);
            }

            return (config.Value, cert.Value);
        }

        return (config.Value, null);
    }

    private static Result<X509Certificate2> ReadCertificate(
        bool useBuiltInCertificateManager,
        string certificatePath,
        string certificatePassword)
    {
        if (!useBuiltInCertificateManager)
        {
            return CertificateHandler.ReadCertificateFromFile(certificatePath, certificatePassword);
        }

        var certificateStoreResult = CertificateHandler.ReadCertificateFromStore();
        if (!certificateStoreResult.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Can't find the built-in certificate![/]");
            var trustCertificateResult = AddCertificate();
            if (trustCertificateResult.IsSuccess)
            {
                return CertificateHandler.ReadCertificateFromStore();
            }

            return trustCertificateResult;
        }

        return certificateStoreResult;
    }

    public static Result AddCertificate()
    {
        if (AnsiConsole.Confirm("Do you want to create and trust a self signed certificate?"))
        {
            Console.WriteLine($"A confirmation prompt will be displayed if the certificate was not previously trusted. Click yes on the prompt to trust the certificate");
            var certificate = CertificateHandler.CreateAndTrustCertificate();

            if (!certificate.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]Failed to create and trust certificate, error: {certificate.Error} [/]");
                AnsiConsole.MarkupLine($"[red]In order to troubleshoot the issue, please use dotnet dev-certs documentation [/]");
                return Result.Fail();
            }
            else if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            {
                AnsiConsole.MarkupLine("[red]Unfortunately, rusting the certificate on Linux distributions automatically is not supported. For instructions on how to manually trust the certificate on your Linux distribution, go to https://aka.ms/dev-certs-trust [/]");
                return Result.Fail();
            }
            else
            {
                return Result.Success();
            }

        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("You need to manually create a certificate and add it as a trusted root certificate to your operation system, add it to the config file and start the application again");
        AnsiConsole.WriteLine();

        return Result.Fail();
    }
}
