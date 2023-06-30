using Microsoft.Azure.Amqp;
using Moq;

namespace Lazvard.Message.Amqp.Server.UnitTests;

public class ExpirationListTests
{

    private readonly ExpirationList target;
    private readonly Mock<Action<BrokerMessage>> onExpiration;
    public ExpirationListTests()
    {
        onExpiration = new Mock<Action<BrokerMessage>>();
        target = new ExpirationList(
            TimeSpan.FromMilliseconds(100),
            onExpiration.Object,
            CancellationToken.None);
    }

    private static BrokerMessage BuildMessage(TimeSpan? lockTime = null)
    {
        var lockedUntil = DateTime.UtcNow + (lockTime ?? TimeSpan.FromMilliseconds(100));
        return new BrokerMessage(new Mock<AmqpMessage>().Object)
            .Lock(Guid.NewGuid(), lockedUntil, "link");
    }

    [Fact]
    public async Task TryAddAndRemoveBeforeExpiration_DonNotCallOnExpiration()
    {
        var brokerMessage = BuildMessage(TimeSpan.FromMilliseconds(200));

        var added = target.TryAdd(brokerMessage);
        Assert.True(added);

        var removed = target.TryRemove(brokerMessage.LockToken, brokerMessage.LockHolderLink);
        Assert.True(removed.IsSuccess);

        await Task.Delay(200);

        onExpiration.Verify(x => x(It.IsAny<BrokerMessage>()), Times.Never);
    }

    [Fact]
    public async Task TryAddAndRemoveAfterExpiration_ReturnFailure()
    {
        var brokerMessage = BuildMessage();

        var added = target.TryAdd(brokerMessage);
        Assert.True(added);

        await Task.Delay(200);

        var removed = target.TryRemove(brokerMessage.LockToken, brokerMessage.LockHolderLink);
        Assert.False(removed.IsSuccess);

        onExpiration.Verify(x => x(It.Is<BrokerMessage>(bm => bm == brokerMessage)), Times.Once);
    }

    [Fact]
    public void TryAdd_AddingExistingItem_ReturnsFalse()
    {
        var brokerMessage = BuildMessage();

        var added1 = target.TryAdd(brokerMessage);
        Assert.True(added1);

        var added2 = target.TryAdd(brokerMessage);
        Assert.False(added2);
    }

    [Fact]
    public void TryToRemove_NonExisting_ReturnFailure()
    {
        var removalResult = target.TryRemove(Guid.NewGuid(), "NotExist");
        Assert.False(removalResult.IsSuccess);
    }

    [Fact]
    public void TryGet_ItemExists_ReturnsMessage()
    {
        var brokerMessage = BuildMessage();
        target.TryAdd(brokerMessage);

        var result = target.TryGet(brokerMessage.LockToken, brokerMessage.LockHolderLink);

        Assert.True(result.IsSuccess);
        Assert.Equal(brokerMessage, result.Value);
    }

    [Fact]
    public void TryGet_ItemDoesNotExists_ReturnsFailure()
    {
        var result = target.TryGet(Guid.NewGuid(), "NotExist");

        Assert.False(result.IsSuccess);
    }
}