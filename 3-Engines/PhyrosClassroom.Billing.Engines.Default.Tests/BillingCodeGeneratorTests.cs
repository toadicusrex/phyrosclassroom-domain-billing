using PhyrosClassroom.Billing.Engines.Default;

namespace PhyrosClassroom.Billing.Engines.Default.Tests;

public sealed class BillingCodeGeneratorTests
{
    [Fact]
    public void GenerateCode_UsesInitialsAndTimestamp()
    {
        var generator = new BillingCodeGenerator();

        var code = generator.GenerateCode(
            "Alice",
            "Bennett",
            new DateTimeOffset(2026, 2, 28, 8, 30, 45, TimeSpan.Zero));

        Assert.Equal("AB-20260228083045", code);
    }
}
