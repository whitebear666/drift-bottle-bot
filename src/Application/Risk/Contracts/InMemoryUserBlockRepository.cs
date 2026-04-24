using Application.Risk.Contracts;
using System.Collections.Concurrent;

namespace Infrastructure.Risk;

public sealed class InMemoryUserBlockRepository : IUserBlockRepository
{
    // key: blockerUserId -> set(blockedAuthorUserId)
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, DateTimeOffset>> _blockedByUser = new();

    // key: blockedAuthorUserId -> set(blockerUserId)
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, DateTimeOffset>> _blockersByAuthor = new();

    public Task<bool> AddBlockAsync(long blockerUserId, long blockedAuthorUserId, DateTimeOffset blockedAtUtc, CancellationToken ct)
    {
        var byUser = _blockedByUser.GetOrAdd(blockerUserId, _ => new ConcurrentDictionary<long, DateTimeOffset>());
        var added = byUser.TryAdd(blockedAuthorUserId, blockedAtUtc);

        // 反向索引用于统计
        var byAuthor = _blockersByAuthor.GetOrAdd(blockedAuthorUserId, _ => new ConcurrentDictionary<long, DateTimeOffset>());
        byAuthor.TryAdd(blockerUserId, blockedAtUtc);

        return Task.FromResult(added);
    }

    public Task<bool> IsBlockedAsync(long blockerUserId, long blockedAuthorUserId, CancellationToken ct)
    {
        if (_blockedByUser.TryGetValue(blockerUserId, out var set))
            return Task.FromResult(set.ContainsKey(blockedAuthorUserId));

        return Task.FromResult(false);
    }

    public Task<int> CountDistinctBlockersAsync(long blockedAuthorUserId, CancellationToken ct)
    {
        if (_blockersByAuthor.TryGetValue(blockedAuthorUserId, out var set))
            return Task.FromResult(set.Count);

        return Task.FromResult(0);
    }
}