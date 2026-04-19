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
    //约定：ReplyDraft 也限制 500 字，拼接规则和瓶子 Draft 类似。
    public bool IsReplying { get; set; }
    public Guid? ReplyThreadId { get; set; }
    public string ReplyDraft { get; set; } = "";

    //这个是拉黑后的操作，严格来说是将只拉黑“这一条会话 thread”（同一个人换新 thread 还能发）
    public HashSet<Guid> BlockedThreadIds { get; set; } = new();
}