using System.Text.Json;
using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence.Default;

public sealed class FileBillingLedgerStore(IOptions<BillingStorageOptions> options) : IBillingLedgerStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<BillingLedger?> GetByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var ledgers = await ReadAllAsync(cancellationToken);
        return ledgers.FirstOrDefault(item => item.RegistrationId == registrationId);
    }

    public async Task<BillingLedger?> GetBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        var ledgers = await ReadAllAsync(cancellationToken);
        return ledgers.FirstOrDefault(item => string.Equals(item.SubjectId, subjectId, StringComparison.Ordinal));
    }

    public Task<IReadOnlyList<BillingLedger>> ListAsync(CancellationToken cancellationToken = default) =>
        ReadAllAsync(cancellationToken).ContinueWith<IReadOnlyList<BillingLedger>>(task => task.Result, cancellationToken);

    public async Task<BillingLedger> SaveAsync(BillingLedger ledger, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var ledgers = (await ReadInternalAsync(filePath, cancellationToken))
                .Where(existing => existing.LedgerId != ledger.LedgerId && existing.RegistrationId != ledger.RegistrationId)
                .ToList();
            ledgers.Add(ledger);

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, ledgers.OrderBy(item => item.HouseholdName).ToList(), SerializerOptions, cancellationToken);
            return ledger;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<IReadOnlyList<BillingLedger>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var filePath = GetFilePath();
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadInternalAsync(filePath, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private string GetFilePath() => Path.Combine(options.Value.BasePath, "billing", "ledgers.json");

    private static async Task<List<BillingLedger>> ReadInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<BillingLedger>>(stream, SerializerOptions, cancellationToken) ?? [];
    }
}
