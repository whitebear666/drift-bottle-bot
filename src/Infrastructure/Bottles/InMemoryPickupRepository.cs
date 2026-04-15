using Application.Bottles.Contracts;
using System.Collections.Concurrent;

namespace Infrastructure.Bottles;

public sealed class InMemoryPickupRepository : IPickupRepository
{
    // key: (pickerUserId, bottleId)
    private readonly ConcurrentDictionary<string, DateTimeOffset> _picked = new();
    private readonly ConcurrentDictionary<Guid, int> _pickupCounts = new();

    private static string Key(long userId, Guid bottleId) => $"{userId}:{bottleId:N}";

    public Task<bool> HasPickedAsync(long pickerUserId, Guid bottleId, CancellationToken ct)
        => Task.FromResult(_picked.ContainsKey(Key(pickerUserId, bottleId)));

    public Task RecordPickupAsync(long pickerUserId, Guid bottleId, DateTimeOffset pickedAtUtc, CancellationToken ct)
    {
        _picked.TryAdd(Key(pickerUserId, bottleId), pickedAtUtc);
        _pickupCounts.AddOrUpdate(bottleId, 1, (_, old) => old + 1);
        return Task.CompletedTask;
    }

    public Task<int> CountPickupsAsync(Guid bottleId, CancellationToken ct)
        => Task.FromResult(_pickupCounts.TryGetValue(bottleId, out var c) ? c : 0);
}