namespace Application.Users;

public sealed class UserState
{
    public required long UserId { get; init; }

    // compose draft
    public bool IsComposing { get; set; }
    public string Draft { get; set; } = "";

    // cooldowns
    public DateTimeOffset? LastPublishAtUtc { get; set; }
    public DateTimeOffset? LastPickupAtUtc { get; set; }

    // quota (Rule B)
    public int PickupQuota { get; set; }
}