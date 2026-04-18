namespace Domain.Conversations;

public sealed class ConversationMessage
{
    public Guid Id { get; init; }
    public Guid ThreadId { get; init; }

    public long FromUserId { get; init; }
    public long ToUserId { get; init; }

    public string Content { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
}