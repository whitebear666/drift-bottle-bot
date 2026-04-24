using Application.Bottles.Contracts;
using Domain.Bottles;
using System.Collections.Concurrent;

namespace Infrastructure.Bottles;

public sealed class InMemoryBottleRepository : IBottleRepository
{
    private readonly ConcurrentDictionary<Guid, Bottle> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _idByNo = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> BottleNoExistsAsync(string bottleNo, CancellationToken ct)
        => Task.FromResult(_idByNo.ContainsKey(bottleNo));

    public Task AddAsync(Bottle bottle, CancellationToken ct)
    {
        _byId[bottle.Id] = bottle;
        _idByNo[bottle.BottleNo] = bottle.Id;
        return Task.CompletedTask;
    }

    public Task<Bottle?> GetByIdAsync(Guid bottleId, CancellationToken ct)
        => Task.FromResult(_byId.TryGetValue(bottleId, out var b) ? b : null);

    public Task<Bottle?> GetByNoAsync(string bottleNo, CancellationToken ct)
    {
        if (_idByNo.TryGetValue(bottleNo, out var id) && _byId.TryGetValue(id, out var b))
            return Task.FromResult<Bottle?>(b);

        return Task.FromResult<Bottle?>(null);
    }

    public Task<IReadOnlyList<Bottle>> ListByAuthorAsync(long authorUserId, int skip, int take, CancellationToken ct)
    {
        var list = _byId.Values
            .Where(b => b.AuthorUserId == authorUserId && !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<Bottle>>(list);
    }

    public Task<bool> DeleteByIdAsync(Guid bottleId, long authorUserId, DateTimeOffset deletedAtUtc, CancellationToken ct)
    {
        if (!_byId.TryGetValue(bottleId, out var b))
            return Task.FromResult(false);

        if (b.AuthorUserId != authorUserId)
            return Task.FromResult(false);

        if (b.IsDeleted)
            return Task.FromResult(true);

        b.IsDeleted = true;
        b.DeletedAt = deletedAtUtc;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Bottle>> ListEligibleForPickupAsync(DateTimeOffset nowUtc, DateTimeOffset expireBeforeUtc, CancellationToken ct)
    {
        var list = _byId.Values
            .Where(b => !b.IsDeleted && b.CreatedAt >= expireBeforeUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<Bottle>>(list);
    }
    //包括已过期/已删除历史也视为全量清除范围：这里会把所有该作者瓶子都 set 为 deleted（即使之前已经 deleted 也会再写一遍 deletedAt）
    public Task<int> DeleteAllByAuthorAsync(long authorUserId, DateTimeOffset deletedAtUtc, CancellationToken ct)
    {
        var count = 0;
        foreach (var b in _byId.Values)
        {
            if (b.AuthorUserId != authorUserId) continue;

            if (!b.IsDeleted)
                count++;

            b.IsDeleted = true;
            b.DeletedAt = deletedAtUtc;
        }

        return Task.FromResult(count);
    }

}