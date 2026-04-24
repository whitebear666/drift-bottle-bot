namespace Application.Risk.Contracts;

public interface IUserBlockRepository
{
    // 去重：同一 blocker 对同一 author 的拉黑只计一次
    Task<bool> AddBlockAsync(long blockerUserId, long blockedAuthorUserId, DateTimeOffset blockedAtUtc, CancellationToken ct);

    Task<bool> IsBlockedAsync(long blockerUserId, long blockedAuthorUserId, CancellationToken ct);

    Task<int> CountDistinctBlockersAsync(long blockedAuthorUserId, CancellationToken ct);
}