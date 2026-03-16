namespace PhyrosClassroom.Billing.Engines;

public interface IBillingCodeGenerator
{
    string GenerateCode(string givenName, string familyName, DateTimeOffset occurredUtc);
}
