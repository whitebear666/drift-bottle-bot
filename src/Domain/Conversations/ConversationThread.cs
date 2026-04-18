namespace Domain.Conversations;

public sealed class ConversationThread
{
    public Guid Id { get; init; }
    public Guid BottleId { get; init; }

    public long AuthorUserId { get; init; }
    public long PickerUserId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset LastMessageAtUtc { get; set; }
}