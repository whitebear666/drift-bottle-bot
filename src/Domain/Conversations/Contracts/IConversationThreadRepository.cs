using Domain.Conversations;

namespace Application.Conversations.Contracts;

public interface IConversationThreadRepository
{
    Task<ConversationThread?> GetByIdAsync(Guid threadId, CancellationToken ct);

    Task<ConversationThread> GetOrCreateAsync(
        Guid bottleId,
        long authorUserId,
        long pickerUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct);

    Task TouchAsync(Guid threadId, DateTimeOffset nowUtc, CancellationToken ct);
}