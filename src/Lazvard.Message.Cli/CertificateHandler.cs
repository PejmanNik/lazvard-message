using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.AspNetCore.Certificates.Generation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public static class CertificateHandler
{
    private const string oidFriendlyName = "Lazvard Message HTTPS development certificate";

    static CertificateHandler()
    {
        CertificateManager.AspNetHttpsOidFriendlyName = oidFriendlyName;
    }

    public static Result<X509Certificate2> ReadCertificateFromFile(string path, string password)
    {
        try
        {
            return new X509Certificate2(path, password);
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public static Result<X509Certificate2> ReadCertificateFromStore()
    {
        try
        {
            var cert = CertificateManager.Instance
                .ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: true, requireExportable: false)
                .FirstOrDefault();

            if (cert is null)
            {
                return Result.Fail();
            }

            var status = CertificateManager.Instance.CheckCertificateState(cert, interactive: false);
            if (!status.Success)
            {
                return Result.Fail();
            }

            if (!CertificateManager.Instance.IsTrusted(cert))
            {
                return Result.Fail();
            }

            return cert;
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public static Result CreateAndTrustCertificate()
    {
        try
        {
            var manager = CertificateManager.Instance;
            var now = DateTimeOffset.Now;
            var result = manager.EnsureAspNetCoreHttpsDevelopmentCertificate(
                notBefore: now,
                notAfter: now.AddYears(1),
                isInteractive: false,
                trust: !RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            if (result == EnsureCertificateResult.Succeeded 
                || result == EnsureCertificateResult.NewHttpsCertificateTrusted
                || result == EnsureCertificateResult.ExistingHttpsCertificateTrusted)
                return Result.Success();

            return Result.Fail(result.ToString());
        }
        catch (Exception)
        {
            return Result.Fail();
        }
    }
}