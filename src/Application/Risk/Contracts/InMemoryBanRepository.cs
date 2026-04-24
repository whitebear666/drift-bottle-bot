using Application.Risk.Contracts;
using System.Collections.Concurrent;

namespace Infrastructure.Risk;

public sealed class InMemoryBanRepository : IBanRepository
{
    private sealed record Ban(DateTimeOffset UntilUtc, string Reason);

    private readonly ConcurrentDictionary<long, Ban> _bans = new();

    public Task<DateTimeOffset?> GetBannedUntilAsync(long userId, CancellationToken ct)
    {
        if (_bans.TryGetValue(userId, out var ban))
            return Task.FromResult<DateTimeOffset?>(ban.UntilUtc);

        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task BanAsync(long userId, DateTimeOffset untilUtc, string reason, CancellationToken ct)
    {
        _bans[userId] = new Ban(untilUtc, reason);
        return Task.CompletedTask;
    }

    public Task UnbanAsync(long userId, CancellationToken ct)
    {
        _bans.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}