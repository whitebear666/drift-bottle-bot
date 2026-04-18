using Application.Conversations.Contracts;
using Domain.Conversations;

namespace Infrastructure.Conversations;

public sealed class InMemoryConversationMessageRepository : IConversationMessageRepository
{
    private readonly List<ConversationMessage> _messages = new();

    public Task AddAsync(ConversationMessage message, CancellationToken ct)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}