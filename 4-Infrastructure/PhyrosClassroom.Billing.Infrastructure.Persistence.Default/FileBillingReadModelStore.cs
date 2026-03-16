using System.Text.Json;
using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence.Default;

public sealed class FileBillingReadModelStore(IOptions<BillingStorageOptions> options) : IBillingReadModelStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task UpsertAsync(BillingReadModel billing, CancellationToken cancellationToken = default)
    {
        var filePath = GetReadModelsPath(options.Value.BasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var billings = await ReadInternalAsync(filePath, cancellationToken);
            billings[billing.BillingId] = billing;

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(
                stream,
                billings.Values.OrderBy(item => item.BillingCode).ToList(),
                SerializerOptions,
                cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<BillingReadModel?> GetByIdAsync(Guid billingId, CancellationToken cancellationToken = default)
    {
        var filePath = GetReadModelsPath(options.Value.BasePath);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var billings = await ReadInternalAsync(filePath, cancellationToken);
            return billings.TryGetValue(billingId, out var billing) ? billing : null;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<BillingReadModel?> GetBySourceReferenceAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken = default)
    {
        var filePath = GetReadModelsPath(options.Value.BasePath);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadInternalAsync(filePath, cancellationToken))
                .Values
                .FirstOrDefault(item =>
                    string.Equals(item.SourceSystem, sourceSystem, StringComparison.Ordinal) &&
                    string.Equals(item.SourceReference, sourceReference, StringComparison.Ordinal));
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<BillingReadModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var filePath = GetReadModelsPath(options.Value.BasePath);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadInternalAsync(filePath, cancellationToken))
                .Values
                .OrderBy(item => item.BillingCode)
                .ToList();
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string GetReadModelsPath(string basePath)
    {
        return Path.Combine(basePath, "billing", "read-models.json");
    }

    private static async Task<Dictionary<Guid, BillingReadModel>> ReadInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        var billings = await JsonSerializer.DeserializeAsync<List<BillingReadModel>>(stream, SerializerOptions, cancellationToken);
        return (billings ?? []).ToDictionary(item => item.BillingId);
    }
}
