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

    public async Task<(long ToUserId, string BottleNo, string Content, Guid ThreadId)> SendAsync(long userId, CancellationToken ct)
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

        await _messages.AddAsync(msg, ct);
        await _threads.TouchAsync(thread.Id, now, ct);

        // 退出回复模式
        state.IsReplying = false;
        state.ReplyThreadId = null;
        state.ReplyDraft = "";
        await _userStates.SaveAsync(state, ct);

        return (toUserId, bottleNo, content, thread.Id);
    }
}