using Lazvard.Message.Amqp.Server.Helpers;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public class CliStartup
{
    public static async Task<(CliConfig, X509Certificate2)> StartAsync()
    {
        if (!Configuration.Exists())
        {
            Console.WriteLine("Can't find config file, creating the init config file...");
            Console.WriteLine();


            await Configuration.CreateDefaultConfigAsync();
            Console.WriteLine("default config file successfully created!");

            var certificateStoreResult = CertificateHandler.ReadCertificateFromStore();
            if (!certificateStoreResult.IsSuccess)
            {
                var addCertificateResult = AddCertificate();
                if (!addCertificateResult.IsSuccess)
                {
                    Environment.Exit(0);
                }
            }
        }

        var config = Configuration.Read();
        if (!config.IsSuccess)
        {
            Console.WriteLine($"Can't load the config file, Please check the syntax");
            Console.WriteLine($"Error: {config.Error}");
            Environment.Exit(0);
        }

        var cert = ReadCertificate(
            config.Value.UseBuiltInCertificateManager,
            config.Value.CertificatePath,
            config.Value.CertificatePassword);

        if (!cert.IsSuccess)
        {
            Console.WriteLine($"Can't load the certificate. please check the certificate or use the built-in certificate manager");
            Console.WriteLine($"In order to troubleshoot the built-in certificate manager please use dotnet dev-certs documentation");
            Console.WriteLine($"Error: {cert.Error}");

            Environment.Exit(0);
        }

        return (config.Value, cert.Value);
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
            var trustCertificateResult = CertificateHandler.CreateAndTrustCertificate();
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
        Console.WriteLine();
        Console.WriteLine("ServiceBus needs a valid certificate to start HTTPs server.");
        Console.WriteLine("You can add the certificate info in the config file,");
        Console.WriteLine("or create a new certificate using built-in certificate manager (powered by dotnet dev-certs)");
        Console.WriteLine();

        Console.Write("Do you want to create and trust a self signed certificate (y/n)?  ");

        var answer = Console.ReadLine();
        if (answer?.StartsWith("y", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine($"A confirmation prompt will be displayed if the certificate was not previously trusted. Click yes on the prompt to trust the certificate");
            var certificate = CertificateHandler.CreateAndTrustCertificate();

            if (!certificate.IsSuccess)
            {
                Console.WriteLine($"Failed to create and trust certificate, error: {certificate.Error}");
                Console.WriteLine($"In order to troubleshoot the issue, please use dotnet dev-certs documentation");
                return Result.Fail();
            }
            else if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            {
                Console.WriteLine("Unfortunately, rusting the certificate on Linux distributions automatically is not supported. For instructions on how to manually trust the certificate on your Linux distribution, go to https://aka.ms/dev-certs-trust");
                return Result.Fail();
            }
            else
            {
                return Result.Success();
            }

        }

        Console.WriteLine();
        Console.WriteLine("You need to manually create a certificate and add it as a trusted root certificate to your operation system, add it to the config file and start the application again");
        Console.WriteLine();

        return Result.Fail();
    }
}
