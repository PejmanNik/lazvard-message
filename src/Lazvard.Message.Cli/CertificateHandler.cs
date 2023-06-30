using Lazvard.Message.Amqp.Server.Helpers;
using Lazvard.Message.Cli.CertificateGeneration;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public static class CertificateHandler
{
    public const string Path = "certificate.pfx";

    public static Result<X509Certificate2> ReadCertificate(string path, string password)
    {
        try
        {
            return new X509Certificate2(Path, password);
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }        
    }

    public static Result CreateAndTrustCertificate(string serverIp, string password)
    {
        var rsaKey = RSA.Create(2048);
        var req = new CertificateRequest("cn=localhost", rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(IPAddress.Parse(serverIp));
        sanBuilder.AddDnsName("localhost");        
        req.CertificateExtensions.Add(sanBuilder.Build());

        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));

        //Create PFX(PKCS #12) with private key
        File.WriteAllBytes(Path, cert.Export(X509ContentType.Pfx, password));

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return Result.Success();
        }

        var manager = CertificateManager.Instance;
        try
        {
            manager.TrustCertificate(cert);
            return Result.Success();
        }
        catch (Exception)
        {
            return Result.Fail();
        }
    }
}