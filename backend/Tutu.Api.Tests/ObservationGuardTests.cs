using Tutu.Api;
using Xunit;

namespace Tutu.Api.Tests;

public class ObservationGuardTests
{
    [Fact]
    public void ObservationId_uses_tutu_prefix()
    {
        Assert.Equal("tutu-orders", PubSubConsole.ObservationId("orders"));
    }

    [Fact]
    public void GuardObservation_rejects_foreign_subscription()
    {
        var ex = Assert.Throws<ConsoleException>(() => PubSubConsole.GuardObservation("orders-worker"));
        Assert.Equal(403, ex.Status);
        Assert.Equal("NotObservation", ex.Error);
    }

    [Fact]
    public void GuardObservation_allows_tutu_prefix()
    {
        PubSubConsole.GuardObservation("tutu-orders");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Dead_letter_starts_at_fifth_attempt(int attempts, bool dead)
    {
        Assert.Equal(dead, DlqPolicy.ShouldDeadLetter(attempts));
    }
}
