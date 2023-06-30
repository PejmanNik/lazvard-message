using Microsoft.Azure.Amqp;

namespace Lazvard.Message.Amqp.Server;

public interface INode
{
    string Name { get; }

    void OnAttachLink(AmqpLink link);
}

public abstract class Node : INode
{
    public string Name { get; }

    public Node(string name)
    {
        Name = name;
    }

    public void OnAttachLink(AmqpLink link)
    {
        if (link.IsReceiver && link is ReceivingAmqpLink receiver)
        {
            OnAttachReceivingLink(receiver);
        }
        else if (link is SendingAmqpLink sender)
        {
            OnAttachSendingLink(sender);
        }
        else
        {
            link.SafeClose(new AmqpException(AmqpErrorCode.NotAllowed, "The link type is not supported"));
        }
    }

    public abstract void OnAttachReceivingLink(ReceivingAmqpLink link);
    public abstract void OnAttachSendingLink(SendingAmqpLink link);
}
