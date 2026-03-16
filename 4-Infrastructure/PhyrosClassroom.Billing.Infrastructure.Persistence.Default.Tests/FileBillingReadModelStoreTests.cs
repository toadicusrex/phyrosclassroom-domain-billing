using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Infrastructure.Persistence.Default;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence.Default.Tests;

public sealed class FileBillingReadModelStoreTests
{
    [Fact]
    public async Task UpsertAsync_PersistsAndReturnsBilling()
    {
        var basePath = Path.Combine(
            Path.GetTempPath(),
            "phyrosclassroom-billing-infrastructure-tests",
            Guid.NewGuid().ToString("N"));

        var store = new FileBillingReadModelStore(
            Options.Create(new BillingStorageOptions
            {
                BasePath = basePath,
            }));

        var billing = new BillingReadModel(
            Guid.NewGuid(),
            "AB-20260228083045",
            "Alice",
            "Bennett",
            new DateTimeOffset(2026, 2, 28, 8, 30, 45, TimeSpan.Zero),
            "registrations:default",
            "registration-123:child:0",
            new DateOnly(2012, 4, 16),
            "6",
            "Sarah Bennett",
            "sarah@example.com",
            false,
            null,
            false,
            [],
            []);

        await store.UpsertAsync(billing);
        var loadedBilling = await store.GetByIdAsync(billing.BillingId);

        Assert.NotNull(loadedBilling);
        Assert.Equal(billing.BillingCode, loadedBilling!.BillingCode);
    }
}
