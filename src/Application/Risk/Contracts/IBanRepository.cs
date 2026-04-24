namespace Application.Risk.Contracts;

public interface IBanRepository
{
    Task<DateTimeOffset?> GetBannedUntilAsync(long userId, CancellationToken ct);

    Task BanAsync(long userId, DateTimeOffset untilUtc, string reason, CancellationToken ct);

    Task UnbanAsync(long userId, CancellationToken ct);
}