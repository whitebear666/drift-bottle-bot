using Application.Conversations.Contracts;
using Domain.Conversations;

namespace Infrastructure.Conversations;

public sealed class InMemoryConversationThreadRepository : IConversationThreadRepository
{
    private readonly Dictionary<Guid, ConversationThread> _threads = new();
    private readonly Dictionary<(Guid BottleId, long PickerUserId), Guid> _index = new();

    public Task<ConversationThread?> GetByIdAsync(Guid threadId, CancellationToken ct)
    {
        _threads.TryGetValue(threadId, out var thread);
        return Task.FromResult(thread);
    }

    public Task<ConversationThread> GetOrCreateAsync(
        Guid bottleId,
        long authorUserId,
        long pickerUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var key = (bottleId, pickerUserId);

        if (_index.TryGetValue(key, out var existingThreadId) &&
            _threads.TryGetValue(existingThreadId, out var existing))
        {
            return Task.FromResult(existing);
        }

        var created = new ConversationThread
        {
            Id = Guid.NewGuid(),
            BottleId = bottleId,
            AuthorUserId = authorUserId,
            PickerUserId = pickerUserId,
            CreatedAtUtc = nowUtc,
            LastMessageAtUtc = nowUtc
        };

        _threads[created.Id] = created;
        _index[key] = created.Id;

        return Task.FromResult(created);
    }

    public Task TouchAsync(Guid threadId, DateTimeOffset nowUtc, CancellationToken ct)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            thread.LastMessageAtUtc = nowUtc;

        return Task.CompletedTask;
    }
}