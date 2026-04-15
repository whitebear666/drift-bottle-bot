namespace Application.Bottles.Contracts;

public interface IPickupRepository
{
    Task<bool> HasPickedAsync(long pickerUserId, Guid bottleId, CancellationToken ct);
    Task RecordPickupAsync(long pickerUserId, Guid bottleId, DateTimeOffset pickedAtUtc, CancellationToken ct);

    Task<int> CountPickupsAsync(Guid bottleId, CancellationToken ct);
}