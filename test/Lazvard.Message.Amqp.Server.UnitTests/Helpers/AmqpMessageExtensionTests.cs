using Microsoft.Azure.Amqp;

namespace Lazvard.Message.Amqp.Server.UnitTests.Helpers;

public class AmqpMessageExtensionTests
{
    public void Clone()
    {
        var message = AmqpMessage.Create();
    }
}
