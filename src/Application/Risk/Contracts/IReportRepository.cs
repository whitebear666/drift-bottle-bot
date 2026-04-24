namespace Application.Risk.Contracts;

public interface IReportRepository
{
    // 去重：同一 reporter 对同一 author 的举报只计一次（即使举报不同瓶子也算一次）
    Task<bool> AddReportAsync(long reporterUserId, long targetAuthorUserId, Guid bottleId, DateTimeOffset reportedAtUtc, CancellationToken ct);

    Task<int> CountDistinctReportersAsync(long targetAuthorUserId, CancellationToken ct);
}