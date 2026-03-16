using System.Text.Json;
using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence.Default;

public sealed class FileBillingEventStore(IOptions<BillingStorageOptions> options) : IBillingEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task AppendAsync(
        Guid billingId,
        IReadOnlyList<BillingEventRecord> events,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetEventsPath(options.Value.BasePath, billingId);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var existingEvents = await ReadInternalAsync(filePath, cancellationToken);
            existingEvents.AddRange(events);

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, existingEvents, SerializerOptions, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<BillingEventRecord>> GetByIdAsync(Guid billingId, CancellationToken cancellationToken = default)
    {
        var filePath = GetEventsPath(options.Value.BasePath, billingId);

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

    private static string GetEventsPath(string basePath, Guid billingId)
    {
        return Path.Combine(basePath, "billing", "events", $"{billingId:N}.json");
    }

    private static async Task<List<BillingEventRecord>> ReadInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        var events = await JsonSerializer.DeserializeAsync<List<BillingEventRecord>>(stream, SerializerOptions, cancellationToken);
        return events ?? [];
    }
}
