using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Models.Tests;

public sealed class BillingAggregateTests
{
    [Fact]
    public void Register_CreatesInitialEventAndReadModel()
    {
        var billingId = Guid.NewGuid();
        var occurredUtc = new DateTimeOffset(2026, 2, 28, 8, 0, 0, TimeSpan.Zero);

        var aggregate = BillingAggregate.Register(
            billingId,
            "AB-20260228080000",
            "Alice",
            "Bennett",
            occurredUtc);

        var readModel = aggregate.ToReadModel();

        Assert.Equal(billingId, aggregate.BillingId);
        Assert.Single(aggregate.Events);
        Assert.Equal("BillingRegistered", aggregate.Events[0].EventType);
        Assert.Equal("Alice", readModel.GivenName);
        Assert.Equal("Bennett", readModel.FamilyName);
        Assert.Equal(occurredUtc, readModel.RegisteredAtUtc);
    }

    [Fact]
    public void RehydrateAt_ReturnsNull_WhenNoEventsExistBeforePointInTime()
    {
        var eventHistory = new[]
        {
            new BillingEventRecord(
                Guid.NewGuid(),
                "BillingRegistered",
                new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.Zero),
                "AB-20260228100000",
                "Alice",
                "Bennett"),
        };

        var aggregate = BillingAggregate.RehydrateAt(
            eventHistory,
            new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero));

        Assert.Null(aggregate);
    }
}
