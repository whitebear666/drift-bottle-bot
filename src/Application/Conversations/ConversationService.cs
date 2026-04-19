using Application.Bottles.Contracts;
using Application.Common;
using Application.Conversations.Contracts;
using Application.Users.Contracts;
using Domain.Conversations;

namespace Application.Conversations;

public sealed class ConversationService
{
    private const int MaxReplyLength = 500;

    private readonly IClock _clock;
    private readonly IBottleRepository _bottles;
    private readonly IPickupRepository _pickups;
    private readonly IUserStateRepository _userStates;
    private readonly IConversationThreadRepository _threads;
    private readonly IConversationMessageRepository _messages;

    public ConversationService(
        IClock clock,
        IBottleRepository bottles,
        IPickupRepository pickups,
        IUserStateRepository userStates,
        IConversationThreadRepository threads,
        IConversationMessageRepository messages)
    {
        _clock = clock;
        _bottles = bottles;
        _pickups = pickups;
        _userStates = userStates;
        _threads = threads;
        _messages = messages;
    }

    public async Task StartFromBottleAsync(long userId, Guid bottleId, CancellationToken ct)
    {
        var bottle = await _bottles.GetByIdAsync(bottleId, ct);
        if (bottle is null || bottle.IsDeleted)
            throw new InvalidOperationException("瓶子不存在或已删除。");

        if (bottle.AuthorUserId == userId)
            throw new InvalidOperationException("不能回复自己写的瓶子。");

        var hasPicked = await _pickups.HasPickedAsync(userId, bottleId, ct);
        if (!hasPicked)
            throw new InvalidOperationException("你没有捞到过这个瓶子，无法回复。");

        var now = _clock.UtcNow;
        var thread = await _threads.GetOrCreateAsync(bottleId, bottle.AuthorUserId, userId, now, ct);

        var state = await _userStates.GetAsync(userId, ct);
        state.IsReplying = true;
        state.ReplyThreadId = thread.Id;
        state.ReplyDraft = "";
        await _userStates.SaveAsync(state, ct);
    }

    public async Task StartFromThreadAsync(long userId, Guid threadId, CancellationToken ct)
    {
        var thread = await _threads.GetByIdAsync(threadId, ct);
        if (thread is null)
            throw new InvalidOperationException("会话不存在或已失效。");

        if (thread.AuthorUserId != userId && thread.PickerUserId != userId)
            throw new InvalidOperationException("你无权参与这个会话。");

        var state = await _userStates.GetAsync(userId, ct);
        state.IsReplying = true;
        state.ReplyThreadId = thread.Id;
        state.ReplyDraft = "";
        await _userStates.SaveAsync(state, ct);
    }

    public async Task AppendDraftAsync(long userId, string line, CancellationToken ct)
    {
        line = (line ?? "").Trim();
        if (string.IsNullOrWhiteSpace(line))
            throw new InvalidOperationException("回复内容不能为空。");

        var state = await _userStates.GetAsync(userId, ct);
        if (!state.IsReplying || state.ReplyThreadId is null)
            throw new InvalidOperationException("你当前不在回复模式。");

        var newText = string.IsNullOrWhiteSpace(state.ReplyDraft)
            ? line
            : state.ReplyDraft + "\n" + line;

        if (newText.Length > MaxReplyLength)
            throw new InvalidOperationException($"匿名回复最多 {MaxReplyLength} 字。");

        state.ReplyDraft = newText;
        await _userStates.SaveAsync(state, ct);
    }

    public async Task CancelAsync(long userId, CancellationToken ct)
    {
        var state = await _userStates.GetAsync(userId, ct);
        state.IsReplying = false;
        state.ReplyThreadId = null;
        state.ReplyDraft = "";
        await _userStates.SaveAsync(state, ct);
    }

    // ===== M3.3：发送匿名回复（两阶段：Peek + Commit）=====
    // 背景：
    // - 之前的 SendAsync 是“一步到位”：会写入消息、touch thread、并清空草稿退出回复模式。
    // - 但现在我们需要在 bot 层做“对方是否屏蔽本会话”的检查。
    // - 如果先 SendAsync 再检查，可能出现：消息没发出去但草稿被清空（体验差）。
    //
    // 解决：
    // 1) PeekSendAsync：只读取当前将要发送的数据（不写库、不清草稿）
    // 2) CommitSendAsync：真正写入消息并清空草稿退出回复模式
    // 3) SendAsync：为了兼容旧调用点，保留为“直接 Commit”（或以后可改成 Peek+Commit）
    public async Task<(long ToUserId, string BottleNo, string Content, Guid ThreadId)> PeekSendAsync(long userId, CancellationToken ct)
    {
        var state = await _userStates.GetAsync(userId, ct);
        if (!state.IsReplying || state.ReplyThreadId is null)
            throw new InvalidOperationException("你当前不在回复模式。");

        var content = (state.ReplyDraft ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("回复内容不能为空。");

        var thread = await _threads.GetByIdAsync(state.ReplyThreadId.Value, ct);
        if (thread is null)
            throw new InvalidOperationException("会话不存在或已失效。");

        if (thread.AuthorUserId != userId && thread.PickerUserId != userId)
            throw new InvalidOperationException("你无权参与这个会话。");

        var toUserId = thread.AuthorUserId == userId ? thread.PickerUserId : thread.AuthorUserId;

        var bottle = await _bottles.GetByIdAsync(thread.BottleId, ct);
        var bottleNo = bottle?.BottleNo ?? "未知编号";

        // 注意：这里不做任何写入，不 touch thread，不清空草稿。
        return (toUserId, bottleNo, content, thread.Id);
    }

    public async Task<(long ToUserId, string BottleNo, string Content, Guid ThreadId)> CommitSendAsync(long userId, CancellationToken ct)
    {
        // Commit 阶段再次读取一遍 state/thread/content，确保使用“提交时刻”的草稿内容。
        // （避免 Peek 后用户又追加了内容，造成发送内容不一致；同时也标准化接口，不传 expectedContent。）
        var state = await _userStates.GetAsync(userId, ct);
        if (!state.IsReplying || state.ReplyThreadId is null)
            throw new InvalidOperationException("你当前不在回复模式。");

        var content = (state.ReplyDraft ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("回复内容不能为空。");

        var thread = await _threads.GetByIdAsync(state.ReplyThreadId.Value, ct);
        if (thread is null)
            throw new InvalidOperationException("会话不存在或已失效。");

        if (thread.AuthorUserId != userId && thread.PickerUserId != userId)
            throw new InvalidOperationException("你无权参与这个会话。");

        var toUserId = thread.AuthorUserId == userId ? thread.PickerUserId : thread.AuthorUserId;

        var bottle = await _bottles.GetByIdAsync(thread.BottleId, ct);
        var bottleNo = bottle?.BottleNo ?? "未知编号";

        var now = _clock.UtcNow;
        var msg = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            FromUserId = userId,
            ToUserId = toUserId,
            Content = content,
            CreatedAtUtc = now
        };

        // 1) 落库消息 + touch thread
        await _messages.AddAsync(msg, ct);
        await _threads.TouchAsync(thread.Id, now, ct);

        // 2) 退出回复模式（清空草稿）
        state.IsReplying = false;
        state.ReplyThreadId = null;
        state.ReplyDraft = "";
        await _userStates.SaveAsync(state, ct);

        return (toUserId, bottleNo, content, thread.Id);
    }

    // 兼容旧调用点：保留 SendAsync，但现在语义是“直接提交发送”
    public async Task<(long ToUserId, string BottleNo, string Content, Guid ThreadId)> SendAsync(long userId, CancellationToken ct)
        => await CommitSendAsync(userId, ct);
}