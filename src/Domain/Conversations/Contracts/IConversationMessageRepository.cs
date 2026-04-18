using Domain.Conversations;

namespace Application.Conversations.Contracts;

public interface IConversationMessageRepository
{
    Task AddAsync(ConversationMessage message, CancellationToken ct);
}