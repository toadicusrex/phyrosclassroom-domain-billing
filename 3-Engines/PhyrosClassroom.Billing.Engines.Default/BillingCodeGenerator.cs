using PhyrosClassroom.Billing.Engines;

namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class BillingCodeGenerator : IBillingCodeGenerator
{
    public string GenerateCode(string givenName, string familyName, DateTimeOffset occurredUtc)
    {
        var initials = $"{GetInitial(givenName)}{GetInitial(familyName)}";
        return $"{initials}-{occurredUtc:yyyyMMddHHmmss}";
    }

    private static char GetInitial(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? 'X' : char.ToUpperInvariant(value.Trim()[0]);
    }
}
