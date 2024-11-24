using Lazvard.Message.Amqp.Server.Helpers;
using System.Security.Cryptography.X509Certificates;

namespace Lazvard.Message.Cli;

public static class CertificateHandler
{
    public static Result<X509Certificate2> ReadCertificateFromFile(string path, string password)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(path, password);
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }
}