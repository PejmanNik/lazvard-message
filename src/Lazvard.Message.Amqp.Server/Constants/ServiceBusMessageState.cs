namespace Lazvard.Message.Amqp.Server.Constants;

//From: https://github.com/Azure/azure-sdk-for-net/blob/79b2f179d8d4a404584784fc86abebfd32887576/sdk/servicebus/Microsoft.Azure.ServiceBus/src/MessageState.cs
public enum MessageState
{
    Active,
    Deferred,
    Scheduled,
}
