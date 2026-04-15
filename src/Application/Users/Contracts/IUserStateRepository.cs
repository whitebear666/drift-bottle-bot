using Application.Users;

namespace Application.Users.Contracts;

public interface IUserStateRepository
{
    Task<UserState> GetAsync(long userId, CancellationToken ct);
    Task SaveAsync(UserState state, CancellationToken ct);
}