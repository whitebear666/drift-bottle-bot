using Application.Risk.Contracts;
using System.Collections.Concurrent;

namespace Infrastructure.Risk;

public sealed class InMemoryReportRepository : IReportRepository
{
    // key: targetAuthorUserId -> set(reporterUserId)
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, DateTimeOffset>> _reportersByAuthor = new();

    public Task<bool> AddReportAsync(long reporterUserId, long targetAuthorUserId, Guid bottleId, DateTimeOffset reportedAtUtc, CancellationToken ct)
    {
        var set = _reportersByAuthor.GetOrAdd(targetAuthorUserId, _ => new ConcurrentDictionary<long, DateTimeOffset>());
        var added = set.TryAdd(reporterUserId, reportedAtUtc);
        return Task.FromResult(added);
    }

    public Task<int> CountDistinctReportersAsync(long targetAuthorUserId, CancellationToken ct)
    {
        if (_reportersByAuthor.TryGetValue(targetAuthorUserId, out var set))
            return Task.FromResult(set.Count);

        return Task.FromResult(0);
    }
}