using Application.Users;
using Application.Users.Contracts;
using System.Collections.Concurrent;

namespace Infrastructure.Users;

public sealed class InMemoryUserStateRepository : IUserStateRepository
{
    private readonly ConcurrentDictionary<long, UserState> _states = new();

    public Task<UserState> GetAsync(long userId, CancellationToken ct)
    {
        var s = _states.GetOrAdd(userId, id => new UserState { UserId = id });
        return Task.FromResult(s);
    }

    public Task SaveAsync(UserState state, CancellationToken ct)
    {
        _states[state.UserId] = state;
        return Task.CompletedTask;
    }
}