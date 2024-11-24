using Lazvard.Message.Amqp.Server.Helpers;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.Amqp.Transaction;
using Microsoft.Azure.Amqp.Transport;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Lazvard.Message.Amqp.Server;

public partial class Broker
{
    private readonly TransportListener transportListener;
    private readonly BrokerConfig brokerSettings;
    private readonly ILogger logger;
    private readonly string containerId;
    private readonly AmqpSettings amqpSettings;
    private readonly ConcurrentDictionary<SequenceNumber, AmqpConnection> connections;
    private readonly Dictionary<string, INode> nodes;

    public Broker(
        BrokerConfig brokerSettings,
        IEnumerable<INode> nodes,
        IEnumerable<TransportListener> transportListeners,
        AmqpSettings amqpSettings,
        ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<Broker>();
        connections = new();
        transportListener = new AmqpTransportListener(transportListeners, amqpSettings);
        this.brokerSettings = brokerSettings;
        this.amqpSettings = amqpSettings;
        this.nodes = nodes
            .ToDictionary(x => x.Name, x => x);

        containerId = $"AmqpBroker-P{Environment.ProcessId}";
        amqpSettings.RuntimeProvider = this;
    }

    public void Start()
    {
        transportListener.Listen(OnAcceptTransport);
    }

    public void Stop()
    {
        transportListener.Close();
    }

    private void OnAcceptTransport(TransportListener listener, TransportAsyncCallbackArgs args)
    {
        var connectionSettings = new AmqpConnectionSettings()
        {
            ContainerId = containerId,
            MaxFrameSize = brokerSettings.MaxFrameSize,
            IdleTimeOut = brokerSettings.ConnectionIdleTimeOut,
        };

        AmqpConnection? connection = null;
        try
        {
            logger.LogTrace("accept new connecting from {Remote}/{Id}", 
                args.Transport.RemoteEndPoint, args.Transport.Identifier);

            connection = this.CreateConnection(
                args.Transport,
                (ProtocolHeader)args.UserToken,
                false,
                amqpSettings,
                connectionSettings);

            connection.BeginOpen(AmqpConstants.DefaultTimeout, this.OnConnectionOpenComplete, connection);
        }
        catch (Exception ex)
        {
            connection?.SafeClose(ex);
        }
    }

    private void OnConnectionOpenComplete(IAsyncResult result)
    {
        if (result.AsyncState is not AmqpConnection connection)
        {
            return;
        }

        try
        {
            logger.LogTrace("open connecting {Identifier}", connection.Identifier);

            connection.EndOpen(result);

            connection.AmqpSettings.RuntimeProvider = this;
            connection.Closed += OnConnectionClose;

            connections.TryAdd(connection.Identifier, connection);
        }
        catch (Exception ex)
        {
            connection.SafeClose(ex);
        }
    }

    private void OnConnectionClose(object? sender, EventArgs e)
    {
        if (sender is AmqpConnection connection)
        {
            logger.LogTrace("close connecting {Identifier}", connection.Identifier);
            connections.TryRemove(connection.Identifier, out _);
        }
    }
}

public partial class Broker : IRuntimeProvider
{
    public IAsyncResult BeginOpenLink(AmqpLink link, TimeSpan timeout, AsyncCallback callback, object state)
    {
        if (link.IsReceiver
            && link is ReceivingAmqpLink receivingLink
            && link.Settings.Target is Coordinator)
        {
            throw new AmqpException(AmqpErrorCode.NotImplemented, "Not supporting the transaction yet.");
        }
        else
        {
            Address address = (link.IsReceiver ?
                 ((Target)link.Settings.Target).Address :
                 ((Source)link.Settings.Source).Address)
                 ?? throw new AmqpException(AmqpErrorCode.InvalidField, "Address not set");

            string nodeName = AddressParser.Parse(address.ToString()).Node;
            if (!nodes.TryGetValue(nodeName, out var node))
            {
                throw new AmqpException(AmqpErrorCode.NotFound, $"Can't find node '{nodeName}'");
            }

            node.OnAttachLink(link);

            return new CompletedAsyncResult(callback, state);
        }

        throw new AmqpException(AmqpErrorCode.NotImplemented, "Not supporting this operation");
    }

    public AmqpConnection CreateConnection(TransportBase transport, ProtocolHeader protocolHeader, bool isInitiator, AmqpSettings amqpSettings, AmqpConnectionSettings connectionSettings)
    {
        return new AmqpConnection(transport, protocolHeader, false, amqpSettings, connectionSettings);
    }

    public AmqpLink CreateLink(AmqpSession session, AmqpLinkSettings settings)
    {
        var isReceiver = settings.IsReceiver();
        if (isReceiver)
        {
            settings.MaxMessageSize = brokerSettings.MaxMessageSize;
            return new ReceivingAmqpLink(session, settings);
        }
        else
        {
            return new SendingAmqpLink(session, settings);
        }
    }

    public AmqpSession CreateSession(AmqpConnection connection, AmqpSessionSettings settings)
    {
        throw new AmqpException(AmqpErrorCode.NotImplemented, "Not supporting the session yet.");
    }

    public void EndOpenLink(IAsyncResult result)
    {
    }
}
