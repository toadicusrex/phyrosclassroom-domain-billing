using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;
using PhyrosClassroom.Billing.Orchestration;
using PhyrosClassroom.Billing.Orchestration.Default;

namespace PhyrosClassroom.Billing.Orchestration.Default.Tests;

public sealed class RegisterBillingUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsBillingAndHydratesCache()
    {
        var eventStore = new FakeBillingEventStore();
        var readModelStore = new FakeBillingReadModelStore();
        var hydratedModelCache = new FakeBillingHydratedModelCache();
        var useCase = new RegisterBillingUseCase(
            new StubBillingCodeGenerator(),
            eventStore,
            readModelStore,
            hydratedModelCache);

        var billing = await useCase.ExecuteAsync(
            new RegisterBillingRequest("Alice", "Bennett"));

        var storedEvents = await eventStore.GetByIdAsync(billing.BillingId);
        var storedReadModel = await readModelStore.GetByIdAsync(billing.BillingId);
        var cachedBilling = await hydratedModelCache.GetAsync(billing.BillingId);

        Assert.Single(storedEvents);
        Assert.NotNull(storedReadModel);
        Assert.NotNull(cachedBilling);
        Assert.Equal("AB-REFERENCE", billing.BillingCode);
        Assert.Equal("Alice", storedReadModel!.GivenName);
    }

    [Fact]
    public async Task ExecuteAsync_RehydratesFromCacheBeforeReadingEventStore()
    {
        var cachedBilling = BillingAggregate.Register(
            Guid.NewGuid(),
            "AB-REFERENCE",
            "Alice",
            "Bennett",
            new DateTimeOffset(2026, 2, 28, 8, 0, 0, TimeSpan.Zero));

        var eventStore = new FakeBillingEventStore();
        var hydratedModelCache = new FakeBillingHydratedModelCache();
        await hydratedModelCache.SetAsync(cachedBilling);

        var useCase = new GetBillingByIdUseCase(eventStore, hydratedModelCache);

        var billing = await useCase.ExecuteAsync(cachedBilling.BillingId);

        Assert.NotNull(billing);
        Assert.Equal(cachedBilling.BillingId, billing!.BillingId);
        Assert.Equal(0, eventStore.ReadCount);
    }

    private sealed class StubBillingCodeGenerator : IBillingCodeGenerator
    {
        public string GenerateCode(string givenName, string familyName, DateTimeOffset occurredUtc)
        {
            return "AB-REFERENCE";
        }
    }

    private sealed class FakeBillingEventStore : IBillingEventStore
    {
        private readonly Dictionary<Guid, List<BillingEventRecord>> _events = [];

        public int ReadCount { get; private set; }

        public Task AppendAsync(Guid billingId, IReadOnlyList<BillingEventRecord> events, CancellationToken cancellationToken = default)
        {
            if (!_events.TryGetValue(billingId, out var eventList))
            {
                eventList = [];
                _events[billingId] = eventList;
            }

            eventList.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BillingEventRecord>> GetByIdAsync(Guid billingId, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<BillingEventRecord>>(
                _events.TryGetValue(billingId, out var eventList) ? eventList : []);
        }
    }

    private sealed class FakeBillingReadModelStore : IBillingReadModelStore
    {
        private readonly Dictionary<Guid, BillingReadModel> _billing = [];

        public Task UpsertAsync(BillingReadModel billing, CancellationToken cancellationToken = default)
        {
            _billing[billing.BillingId] = billing;
            return Task.CompletedTask;
        }

        public Task<BillingReadModel?> GetByIdAsync(Guid billingId, CancellationToken cancellationToken = default)
        {
            _billing.TryGetValue(billingId, out var billing);
            return Task.FromResult(billing);
        }

        public Task<BillingReadModel?> GetBySourceReferenceAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken = default)
        {
            var billing = _billing.Values.FirstOrDefault(item =>
                string.Equals(item.SourceSystem, sourceSystem, StringComparison.Ordinal) &&
                string.Equals(item.SourceReference, sourceReference, StringComparison.Ordinal));
            return Task.FromResult(billing);
        }

        public Task<IReadOnlyList<BillingReadModel>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingReadModel>>(_billing.Values.ToList());
        }
    }

    private sealed class FakeBillingHydratedModelCache : IBillingHydratedModelCache
    {
        private readonly Dictionary<Guid, BillingAggregate> _billing = [];

        public Task<BillingAggregate?> GetAsync(Guid billingId, CancellationToken cancellationToken = default)
        {
            _billing.TryGetValue(billingId, out var billing);
            return Task.FromResult(billing);
        }

        public Task SetAsync(BillingAggregate billing, CancellationToken cancellationToken = default)
        {
            _billing[billing.BillingId] = billing;
            return Task.CompletedTask;
        }
    }
}
