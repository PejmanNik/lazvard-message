using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public class CliStartup
{
    public static async Task<(CliConfig, X509Certificate2)> StartAsync()
    {
        if (!Configuration.Exists())
        {
            Console.WriteLine("Can't find config file, creating the init config file...");

            var (certificatePath, certificatePassword, isTrusted) = AddCertificate("127.0.0.1");
            await Configuration.CreateDefaultConfigAsync(certificatePath, certificatePassword);

            Console.WriteLine();
            Console.WriteLine("config file successfully created!");

            if (!isTrusted)
            {
                Environment.Exit(0);
            }
        }

        var config = Configuration.Read();
        if (!config.IsSuccess)
        {
            Console.WriteLine($"Can't load the config file, Please check the syntax");
            Console.WriteLine($"Error: {config.Error}");
            Environment.Exit(0);
        }

        if (string.IsNullOrEmpty(config.Value.CertificatePath))
        {
            Console.WriteLine("There is no certificate path in the config file");

            var (certificatePath, certificatePassword, isTrusted) = AddCertificate("127.0.0.1");
            config.Value.CertificatePath = certificatePath;
            config.Value.CertificatePassword = certificatePassword;

            await Configuration.WriteAsync(config.Value);

            if (!isTrusted)
            {
                Environment.Exit(0);
            }
        }

        var cert = CertificateHandler.ReadCertificate(config.Value.CertificatePath, config.Value.CertificatePassword);
        if (!cert.IsSuccess)
        {
            Console.WriteLine($"Can't load the certificate. please check the certificate or removed the certificatePath from config file");
            Console.WriteLine($"Error: {cert.Error}");

            Environment.Exit(0);
        }

        return (config.Value, cert.Value);
    }

    public static (string certificatePath, string certificatePasswor, bool isTrusted) AddCertificate(string ip)
    {
        Console.WriteLine();
        Console.WriteLine("ServiceBus needs a valid certificate to start HTTPs server.");
        Console.WriteLine("You can add the certificate info in the config file, or create a new one");
        Console.WriteLine();

        Console.Write("Do you want to create and trust a self signed certificate (y/n)?  ");

        var answer = Console.ReadLine();
        if (answer?.StartsWith("y", StringComparison.OrdinalIgnoreCase) == true)
        {
            var password = $"{Guid.NewGuid()}-{Guid.NewGuid()}";
            var certificate = CertificateHandler.CreateAndTrustCertificate(ip, password);

            if (!certificate.IsSuccess)
            {
                Console.WriteLine("Failed to create and trust certificate");
                return (string.Empty, string.Empty, false);
            }
            else if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            {
                Console.WriteLine("Unfortunately, it is not possible to add the created certificate (whose path can be found in the config file) to the trusted certificate store in the Linux operating system");
                Console.WriteLine();
                Console.WriteLine("You need to manually add this certificate as a trusted root certificate to your operation system and start the application again");

                return (CertificateHandler.Path, password, false);
            }
            else
            {
                return (CertificateHandler.Path, password, true);
            }

        }

        Console.WriteLine();
        Console.WriteLine("You need to manually create a certificate and add it as a trusted root certificate to your operation system, add it to the config file and start the application again");
        Console.WriteLine();

        return (string.Empty, string.Empty, false);
    }
}
