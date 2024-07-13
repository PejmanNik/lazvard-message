using Azure.Messaging.ServiceBus;

namespace Lazvard.Message.Amqp.Server.IntegrationTests;

public sealed class ClientFixture : IAsyncDisposable
{
    public readonly ServiceBusClient Client;

    public ClientFixture()
    {
        string connectionString = "Endpoint=sb://localhost:5671/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=1;UseDevelopmentEmulator=true;";
        Client = new(connectionString);
    }

    public ValueTask DisposeAsync()
    {
        return Client.DisposeAsync();
    }
}

public interface IClientFixture : IClassFixture<ClientFixture>
{
}
