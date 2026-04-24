using Domain.Bottles;

namespace Application.Bottles.Contracts;

public interface IBottleRepository
{
    Task<bool> BottleNoExistsAsync(string bottleNo, CancellationToken ct);
    Task AddAsync(Bottle bottle, CancellationToken ct);

    Task<Bottle?> GetByIdAsync(Guid bottleId, CancellationToken ct);
    Task<Bottle?> GetByNoAsync(string bottleNo, CancellationToken ct);

    Task<IReadOnlyList<Bottle>> ListByAuthorAsync(long authorUserId, int skip, int take, CancellationToken ct);

    Task<bool> DeleteByIdAsync(Guid bottleId, long authorUserId, DateTimeOffset deletedAtUtc, CancellationToken ct);

    Task<IReadOnlyList<Bottle>> ListEligibleForPickupAsync(DateTimeOffset nowUtc, DateTimeOffset expireBeforeUtc, CancellationToken ct);
    //Task DeleteAllByAuthorAsync(long authorUserId, DateTimeOffset now, CancellationToken ct);
    Task<int> DeleteAllByAuthorAsync(long authorUserId, DateTimeOffset deletedAtUtc, CancellationToken ct);
}