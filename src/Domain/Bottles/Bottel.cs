namespace Domain.Bottles;

public sealed class Bottle
{
    public required Guid Id { get; init; }
    public required string BottleNo { get; init; } // YYYYMMDD-xxxxxxxx
    public required long AuthorUserId { get; init; } // Telegram user id
    public required string Content { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}