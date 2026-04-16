using Application.Bottles.Contracts;
using Application.Common;
using Application.Users.Contracts;
using Domain.Bottles;
using System.Security.Cryptography;

namespace Application.Bottles;

public sealed class BottleService
{
    private const int MaxBottleLength = 500;
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ExpireAfter = TimeSpan.FromDays(90);

    private readonly IClock _clock;
    private readonly IBottleRepository _bottles;
    private readonly IPickupRepository _pickups;
    private readonly IUserStateRepository _users;

    public BottleService(IClock clock, IBottleRepository bottles, IPickupRepository pickups, IUserStateRepository users)
    {
        _clock = clock;
        _bottles = bottles;
        _pickups = pickups;
        _users = users;
    }

    public async Task StartComposeAsync(long userId, CancellationToken ct)
    {
        var s = await _users.GetAsync(userId, ct);
        s.IsComposing = true;
        s.Draft = "";
        await _users.SaveAsync(s, ct);
    }

    public async Task AppendDraftAsync(long userId, string line, CancellationToken ct)
    {
        var s = await _users.GetAsync(userId, ct);
        if (!s.IsComposing)
            throw new InvalidOperationException("你还没有开始编辑瓶子。请先点击“开始发一个瓶子”。");

        var candidate = string.IsNullOrEmpty(s.Draft) ? line : (s.Draft + "\n" + line);

        if (candidate.Length > MaxBottleLength)
            throw new InvalidOperationException($"瓶子内容最多 {MaxBottleLength} 字。当前已超出。");

        s.Draft = candidate;
        await _users.SaveAsync(s, ct);
    }

    public async Task<(Guid bottleId, string bottleNo, int pickupQuota)> PublishAsync(long userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var s = await _users.GetAsync(userId, ct);

        if (!s.IsComposing)
            throw new InvalidOperationException("你还没有开始编辑瓶子。请先点击“开始发一个瓶子”。");

        /*if (s.LastPublishAtUtc is { } last && now - last < Cooldown)
            throw new InvalidOperationException("发布太频繁了，请 1 分钟后再试。");*/
        if (s.LastPublishAtUtc is { } last)
        {
            var remain = Cooldown - (now - last);
            if (remain > TimeSpan.Zero)
                throw new InvalidOperationException($"发布太频繁了，请 {Math.Ceiling(remain.TotalSeconds)} 秒后再试。");
        }

        var content = (s.Draft ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("不允许发布空瓶子。");

        if (content.Length > MaxBottleLength)
            throw new InvalidOperationException($"瓶子内容最多 {MaxBottleLength} 字。");

        var bottleNo = await GenerateBottleNoAsync(now, ct);

        var bottle = new Bottle
        {
            Id = Guid.NewGuid(),
            BottleNo = bottleNo,
            AuthorUserId = userId,
            Content = content,
            CreatedAt = now,
            IsDeleted = false
        };

        await _bottles.AddAsync(bottle, ct);

        // quota +1
        s.PickupQuota += 1;

        // exit compose
        s.IsComposing = false;
        s.Draft = "";
        s.LastPublishAtUtc = now;

        await _users.SaveAsync(s, ct);

        return (bottle.Id, bottle.BottleNo, s.PickupQuota);
    }

    public async Task<bool> DeleteMyBottleAsync(long userId, Guid bottleId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        return await _bottles.DeleteByIdAsync(bottleId, userId, now, ct);
    }

    public async Task<(Guid bottleId, string bottleNo, string content, int pickupCount, int pickupQuota)> PickupAsync(long userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var s = await _users.GetAsync(userId, ct);

        /*if (s.LastPickupAtUtc is { } last && now - last < Cooldown)
            throw new InvalidOperationException("捞瓶子太频繁了，请 1 分钟后再试。");*/
        if (s.LastPickupAtUtc is { } last)
        {
            var remain = Cooldown - (now - last);
            if (remain > TimeSpan.Zero)
                throw new InvalidOperationException($"捞瓶子太频繁了，请 {Math.Ceiling(remain.TotalSeconds)} 秒后再试。");
        }

        if (s.PickupQuota <= 0)
            throw new InvalidOperationException("你当前没有捞取次数。先发布一个瓶子，才能捞一个瓶子。");

        var expireBefore = now - ExpireAfter;

        var candidates = await _bottles.ListEligibleForPickupAsync(now, expireBefore, ct);

        // filter: not self, not already picked
        var eligible = new List<Bottle>();
        foreach (var b in candidates)
        {
            if (b.AuthorUserId == userId) continue;
            if (await _pickups.HasPickedAsync(userId, b.Id, ct)) continue;
            eligible.Add(b);
        }

        if (eligible.Count == 0)
            throw new InvalidOperationException("现在没有可捞的瓶子（可能都被你捞过/或已过期/或只剩你自己的）。");

        // random pick
        var picked = eligible[RandomNumberGenerator.GetInt32(eligible.Count)];

        await _pickups.RecordPickupAsync(userId, picked.Id, now, ct);

        // consume quota only on success
        s.PickupQuota -= 1;
        s.LastPickupAtUtc = now;
        await _users.SaveAsync(s, ct);

        var count = await _pickups.CountPickupsAsync(picked.Id, ct);

        return (picked.Id, picked.BottleNo, picked.Content, count, s.PickupQuota);
    }

    public async Task<IReadOnlyList<(Guid id, string bottleNo)>> ListMyBottlesAsync(long userId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 5;

        var skip = (page - 1) * pageSize;
        var items = await _bottles.ListByAuthorAsync(userId, skip, pageSize, ct);
        return items.Select(b => (b.Id, b.BottleNo)).ToList();
    }

    private async Task<string> GenerateBottleNoAsync(DateTimeOffset nowUtc, CancellationToken ct)
    {
        var datePart = nowUtc.ToString("yyyyMMdd");

        // retry a few times if collision
        for (var i = 0; i < 10; i++)
        {
            var suffix = RandomSuffix(8);
            var no = $"{datePart}-{suffix}";

            if (!await _bottles.BottleNoExistsAsync(no, ct))
                return no;
        }

        throw new InvalidOperationException("生成瓶子编号失败，请重试。");
    }

    private static string RandomSuffix(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> buf = stackalloc char[length];

        for (var i = 0; i < length; i++)
            buf[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];

        return new string(buf);

    }
    public async Task<(string bottleNo, string content, int pickupCount)> GetMyBottleDetailAsync(long userId, Guid bottleId, CancellationToken ct)
    {
        var b = await _bottles.GetByIdAsync(bottleId, ct);
        if (b is null || b.IsDeleted)
            throw new InvalidOperationException("瓶子不存在或已删除。");

        if (b.AuthorUserId != userId)
            throw new InvalidOperationException("你无权查看这个瓶子。");

        var count = await _pickups.CountPickupsAsync(bottleId, ct);
        return (b.BottleNo, b.Content, count);
    }
}