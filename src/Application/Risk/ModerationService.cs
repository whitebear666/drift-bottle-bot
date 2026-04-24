using Application.Bottles.Contracts;
using Application.Common;
using Application.Risk.Contracts;
using Application.Users.Contracts;

namespace Application.Risk;

public sealed class ModerationService
{
    private static readonly TimeSpan BanDuration = TimeSpan.FromDays(14);
    private const int ReportBanThreshold = 6;
    private const int BlockPurgeThreshold = 10;

    private readonly IClock _clock;
    private readonly IBottleRepository _bottles;
    private readonly IUserStateRepository _users;
    private readonly IReportRepository _reports;
    private readonly IUserBlockRepository _blocks;
    private readonly IBanRepository _bans;

    public ModerationService(
        IClock clock,
        IBottleRepository bottles,
        IUserStateRepository users,
        IReportRepository reports,
        IUserBlockRepository blocks,
        IBanRepository bans)
    {
        _clock = clock;
        _bottles = bottles;
        _users = users;
        _reports = reports;
        _blocks = blocks;
        _bans = bans;
    }

    public async Task EnsureNotBannedAsync(long userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var until = await _bans.GetBannedUntilAsync(userId, ct);
        if (until is { } u && u > now)
            throw new InvalidOperationException($"你已被封禁，解封时间：{u:yyyy-MM-dd HH:mm} UTC");
    }

    public async Task<(bool Added, int DistinctReporters, bool Banned)> ReportBottleAsync(long reporterUserId, Guid bottleId, CancellationToken ct)
    {
        await EnsureNotBannedAsync(reporterUserId, ct);

        var bottle = await _bottles.GetByIdAsync(bottleId, ct);
        if (bottle is null || bottle.IsDeleted)
            throw new InvalidOperationException("瓶子不存在或已删除。");

        if (bottle.AuthorUserId == reporterUserId)
            throw new InvalidOperationException("不能举报自己的瓶子。");

        var now = _clock.UtcNow;

        var added = await _reports.AddReportAsync(reporterUserId, bottle.AuthorUserId, bottleId, now, ct);
        var cnt = await _reports.CountDistinctReportersAsync(bottle.AuthorUserId, ct);

        var banned = false;
        if (cnt >= ReportBanThreshold)
        {
            await _bans.BanAsync(bottle.AuthorUserId, now + BanDuration, $"被不同用户举报达到阈值：{cnt}", ct);
            banned = true;
        }

        return (added, cnt, banned);
    }

    public async Task<(bool Added, int DistinctBlockers, bool Purged, bool Banned)> BlockBottleAuthorAsync(long blockerUserId, Guid bottleId, CancellationToken ct)
    {
        await EnsureNotBannedAsync(blockerUserId, ct);

        var bottle = await _bottles.GetByIdAsync(bottleId, ct);
        if (bottle is null || bottle.IsDeleted)
            throw new InvalidOperationException("瓶子不存在或已删除。");

        if (bottle.AuthorUserId == blockerUserId)
            throw new InvalidOperationException("不能拉黑自己。");

        var now = _clock.UtcNow;

        // 用户自身黑名单（用于捞瓶过滤）
        var state = await _users.GetAsync(blockerUserId, ct);
        var addedToState = state.BlockedUserIds.Add(bottle.AuthorUserId);
        await _users.SaveAsync(state, ct);

        // 全局去重/统计（用于阈值处罚）
        var addedToRepo = await _blocks.AddBlockAsync(blockerUserId, bottle.AuthorUserId, now, ct);

        var added = addedToRepo || addedToState;

        var cnt = await _blocks.CountDistinctBlockersAsync(bottle.AuthorUserId, ct);

        var purged = false;
        var banned = false;

        if (cnt >= BlockPurgeThreshold)
        {
            await _bottles.DeleteAllByAuthorAsync(bottle.AuthorUserId, now, ct);
            purged = true;

            await _bans.BanAsync(bottle.AuthorUserId, now + BanDuration, $"被不同用户拉黑达到阈值：{cnt}", ct);
            banned = true;
        }

        return (added, cnt, purged, banned);
    }
}