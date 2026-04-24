using Application.Bottles;
using Application.Conversations;
using Application.Risk;
using Application.Users.Contracts;
using Bot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;


namespace Bot.Commands;

public sealed class CallbackCommand : ITelegramCallbackCommand
{
    private readonly BottleService _service;
    private readonly ConversationService _conversations;
    private const int PageSize = 5;
    private readonly IUserStateRepository _userStates;
    private readonly ModerationService _moderation;


    public CallbackCommand(BottleService service,ConversationService conversations,IUserStateRepository userStates,ModerationService moderation)
    {
        _service = service;
        _conversations = conversations;
        _userStates = userStates;
        _moderation = moderation;
    }

    public bool CanHandle(CallbackQuery callbackQuery)
        => callbackQuery.Data == "noop"
        || callbackQuery.Data == "draft.publish"
        || callbackQuery.Data == "reply.send"
        || callbackQuery.Data == "reply.cancel"
        || callbackQuery.Data?.StartsWith("todo.report:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("todo.block:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("bottle.report.todo:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("bottle.block.todo:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("bottle.report:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("bottle.block:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("thread.report.todo:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("todo.report.thread:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("todo.block.thread:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("thread.block.confirm:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("thread.block:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("user.block.permanent.confirm:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("bottle.reply:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("thread.reply:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("bottle.delete:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("mybottles.page:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("mybottles.view:", StringComparison.OrdinalIgnoreCase) == true;

    public async Task HandleAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken ct)
    {
        var data = callbackQuery.Data!;
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message!.Chat.Id;


        try
        {
            // ===== 通用 noop(无操作) =====
            if (data == "noop")
            {
                await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
                return;
            }

            // ===== M3.2：进入匿名回复（从 bottleId）=====
            if (data.StartsWith("bottle.reply:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["bottle.reply:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                await _conversations.StartFromBottleAsync(userId, bottleId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, "请输入回复内容", cancellationToken: ct);

                await bot.SendMessage(
                    chatId,
                    "请输入匿名回复内容（可多条）。输入完成后点“发送”，或点“取消”。",
                    cancellationToken: ct);

                return;
            }
            // ===== M3未实现：举报/拉黑 这个是捞到时的举报拉黑 26.4.19todo =====

            // 新入口（推荐）：bottle.report.todo / bottle.block.todo
            if (data.StartsWith("bottle.report.todo:", StringComparison.OrdinalIgnoreCase)
                || data.StartsWith("bottle.block.todo:", StringComparison.OrdinalIgnoreCase))
            {
                await bot.AnswerCallbackQuery(callbackQuery.Id, "该功能尚未实现。", cancellationToken: ct);
                return;
            }

            // 旧入口：todo.report / todo.block（兼容历史消息按钮）
            // 说明：早期版本使用 todo.report:{bottleId} / todo.block:{bottleId}
            // 现在已更名为 bottle.report.todo / bottle.block.todo，避免和 thread/user 的 todo 回调混淆。
            if (data.StartsWith("todo.report:", StringComparison.OrdinalIgnoreCase)
                || data.StartsWith("todo.block:", StringComparison.OrdinalIgnoreCase))
            {
                await bot.AnswerCallbackQuery(callbackQuery.Id, "该入口已升级（仍未实现），请以最新按钮为准。", cancellationToken: ct);
                return;
            }
            // ===== 这个是回复时的举报拉黑 26.4.19todo ======

            // 1) 举报 thread：仍未实现
            if (data.StartsWith("todo.report.thread:", StringComparison.OrdinalIgnoreCase))
            {
                await bot.AnswerCallbackQuery(callbackQuery.Id, "该功能尚未实现。", cancellationToken: ct);
                return;
            }

            // 2) 旧的“拉黑 thread”入口：兼容历史消息按钮（已升级）
            // 说明：旧按钮 callback 是 todo.block.thread:{threadId}，新流程是 thread.block.confirm:{threadId} -> thread.block:{threadId}
            // 为了避免用户点击旧消息按钮看到“未实现”而困惑，这里给出明确提示。
            if (data.StartsWith("todo.block.thread:", StringComparison.OrdinalIgnoreCase))
            {
                await bot.AnswerCallbackQuery(callbackQuery.Id, "该入口已升级，请使用消息里的“拉黑”按钮重新操作。", cancellationToken: ct);
                return;
            }
            // ===== M3.3：拉黑二次确认（thread 级）=====
            // 说明：Telegram 的 AnswerCallbackQuery(…, showAlert:true) 不能带按钮。
            // 所以这里通过“发送一条消息 + inline buttons”来实现二次确认。
            if (data.StartsWith("thread.block.confirm:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["thread.block.confirm:".Length..];
                if (!Guid.TryParse(idStr, out var threadId))
                    throw new InvalidOperationException("无效的会话编号。");

                await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

                await bot.SendMessage(
                    chatId,
                    "你要屏蔽哪一种？\n\n" +
                    "1) 仅屏蔽本会话（推荐）：只对当前会话 thread 生效。\n" +
                    "2) 永久屏蔽此人：后续将扩展为永久拉黑（目前未实现）。",
                    replyMarkup: TelegramButtons.ConfirmBlock(threadId),
                    cancellationToken: ct);

                return;
            }
            // ===== M3.3：执行“拉黑本会话”（thread 级屏蔽）=====
            // 约定：只拉黑“这一条会话 thread”（同一个人换新 thread 还能发）
            // 实现：把 threadId 记录到【当前用户】的 UserState.BlockedThreadIds 中。
            // 后续当【对方】尝试在同一个 threadId 里继续发送匿名回复时，我们会在 reply.send 里拦截并提示发送失败。
            if (data.StartsWith("thread.block:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["thread.block:".Length..];
                if (!Guid.TryParse(idStr, out var threadId))
                    throw new InvalidOperationException("无效的会话编号。");

                // 1) 读取当前用户状态（当前用户=点击“拉黑本会话”的那个人）
                var state = await _userStates.GetAsync(userId, ct);

                // 2) 写入“已屏蔽的 threadId 列表”
                // HashSet 的 Add 是幂等的：重复 Add 同一个 threadId 也不会报错，但这里我们只 Add 一次即可。
                state.BlockedThreadIds.Add(threadId);

                // 3) 保存用户状态
                // 注意：这里调用的是 Application.Users.Contracts.IUserStateRepository.SaveAsync(state, ct)
                // 作用：把修改后的 state 持久化/保存回仓库（具体存哪取决于你的实现：内存/文件/数据库）。
                await _userStates.SaveAsync(state, ct);

                // 4) 交互反馈：弹出回调提示 + 再发一条确认消息
                await bot.AnswerCallbackQuery(callbackQuery.Id, "已屏蔽本会话。", cancellationToken: ct);

                await bot.SendMessage(
                    chatId,
                    "已屏蔽本会话（仅对当前会话 thread 生效）。",
                    replyMarkup: BotMenus.MainMenu(),
                    cancellationToken: ct);

                return;
            }

            // ===== M3.3：永久拉黑（TODO，占位）=====
            // 说明：永久拉黑需要能定位对方 userId 并持久化（后续里程碑再做）。
            // 当前里程碑只实现 thread 级“屏蔽本会话”，所以这里先提示“未实现”。
            if (data.StartsWith("user.block.permanent.confirm:", StringComparison.OrdinalIgnoreCase))
            {
                await bot.AnswerCallbackQuery(callbackQuery.Id, "该功能尚未实现。", cancellationToken: ct);
                return;
            }

            // ===== M3.2：继续匿名回复（从 threadId）=====
            if (data.StartsWith("thread.reply:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["thread.reply:".Length..];
                if (!Guid.TryParse(idStr, out var threadId))
                    throw new InvalidOperationException("无效的会话编号。");

                await _conversations.StartFromThreadAsync(userId, threadId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, "请输入回复内容", cancellationToken: ct);

                await bot.SendMessage(
                    chatId,
                    "请输入匿名回复内容（可多条）。输入完成后点“发送”，或点“取消”。",
                    cancellationToken: ct);

                return;
            }

            // ===== M3.2：发送匿名回复 =====
            if (data == "reply.send")
            {
                // 1) 预检：先拿到要发给谁、threadId 和内容（不清草稿、不写消息）
                var (toUserId, bottleNo, content, threadId) = await _conversations.PeekSendAsync(userId, ct);

                // ===== M3.3：发送前检查：对方是否已屏蔽本会话 =====
                // 注意：thread 级屏蔽只影响同一个 threadId。
                // 这里读取接收方（toUserId）的 UserState，判断是否包含该 threadId。
                var toState = await _userStates.GetAsync(toUserId, ct);
                if (toState.BlockedThreadIds.Contains(threadId))
                {
                    await bot.AnswerCallbackQuery(callbackQuery.Id, "发送失败", cancellationToken: ct);

                    await bot.SendMessage(
                        chatId,
                        "发送失败：对方已屏蔽本会话，你无法继续在该会话中匿名回复。\n\n" +
                        "提示：你的草稿未被清空，你可以选择“取消”退出，或复制内容后再处理。",
                        replyMarkup: BotMenus.MainMenu(),
                        cancellationToken: ct);

                    return;
                }

                // 2) 真正提交：写入消息 + 清空草稿 + 退出回复模式
                // 注意：CommitSendAsync 会再次读取草稿内容并提交（以提交时刻为准）。
                await _conversations.CommitSendAsync(userId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, "已发送", cancellationToken: ct);

                // 给对方推送匿名消息（重点：不暴露任何身份信息）
                await bot.SendMessage(
                    toUserId,
                    $"收到一条匿名回复（瓶子 {bottleNo}）：\n\n{content}",
                    replyMarkup: TelegramButtons.IncomingAnonymousMessage(threadId),
                    cancellationToken: ct);

                // 给自己确认一下，并把当前消息的按钮去掉（可选）
                await bot.SendMessage(chatId, "匿名回复已发送。", replyMarkup: BotMenus.MainMenu(), cancellationToken: ct);

                if (callbackQuery.Message is not null)
                    await bot.EditMessageReplyMarkup(chatId, callbackQuery.Message.MessageId, replyMarkup: null, cancellationToken: ct);

                return;
            }

            // ===== M3.2：取消匿名回复 =====
            if (data == "reply.cancel")
            {
                await _conversations.CancelAsync(userId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, "已取消", cancellationToken: ct);

                await bot.SendMessage(chatId, "已取消匿名回复。", replyMarkup: BotMenus.MainMenu(), cancellationToken: ct);

                if (callbackQuery.Message is not null)
                    await bot.EditMessageReplyMarkup(chatId, callbackQuery.Message.MessageId, replyMarkup: null, cancellationToken: ct);

                return;
            }

            // ===== 你原来的逻辑：draft.publish =====
            if (data == "draft.publish")
            {
                try
                {
                    var (bottleId, bottleNo, quota) = await _service.PublishAsync(userId, ct);

                    await bot.EditMessageReplyMarkup(chatId, callbackQuery.Message!.MessageId, replyMarkup: null, cancellationToken: ct);
                    await bot.AnswerCallbackQuery(callbackQuery.Id, "已发布", cancellationToken: ct);

                    await bot.SendMessage(
                        chatId,
                        $"已扔出一个瓶子：{bottleNo}\n当前可捞次数：{quota}",
                        replyMarkup: TelegramButtons.DeleteMyBottle(bottleId),
                        cancellationToken: ct);

                    await bot.EditMessageReplyMarkup(chatId, callbackQuery.Message!.MessageId, replyMarkup: null, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    await bot.AnswerCallbackQuery(callbackQuery.Id, ex.Message, cancellationToken: ct);
                }

                return;
            }

            //M4.1 举报的实现

            if (data.StartsWith("bottle.report:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["bottle.report:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                var (added, cnt, banned) = await _moderation.ReportBottleAsync(userId, bottleId, ct);

                await bot.AnswerCallbackQuery(
                    callbackQuery.Id,
                    added ? "已举报" : "你已举报过该作者（不重复计数）",
                    cancellationToken: ct);

                if (banned)
                {
                    await bot.SendMessage(
                        chatId,
                        $"举报已记录。该作者被不同用户举报人数：{cnt}（已达到封禁阈值，已封禁 14 天）",
                        replyMarkup: BotMenus.MainMenu(),
                        cancellationToken: ct);
                }
                else
                {
                    await bot.SendMessage(
                        chatId,
                        $"举报已记录。该作者被不同用户举报人数：{cnt}/6",
                        replyMarkup: BotMenus.MainMenu(),
                        cancellationToken: ct);
                }

                return;
            }

            //M4.1 拉黑作者

            if (data.StartsWith("bottle.block:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["bottle.block:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                var (added, cnt, purged, banned) = await _moderation.BlockBottleAuthorAsync(userId, bottleId, ct);

                await bot.AnswerCallbackQuery(
                    callbackQuery.Id,
                    added ? "已拉黑作者" : "你已拉黑过该作者（不重复计数）",
                    cancellationToken: ct);

                var msg = $"已拉黑作者。该作者被不同用户拉黑人数：{cnt}/10";

                if (purged)
                    msg += "\n\n处罚触发：该作者所有瓶子已被清除。";
                if (banned)
                    msg += "\n处罚触发：该作者已封禁 14 天。";//标准的事件写法，订阅委托的结果

                await bot.SendMessage(chatId, msg, replyMarkup: BotMenus.MainMenu(), cancellationToken: ct);
                return;
            }








            // ===== 你原来的逻辑：bottle.delete =====
            if (data.StartsWith("bottle.delete:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["bottle.delete:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                var ok = await _service.DeleteMyBottleAsync(userId, bottleId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, ok ? "已删除" : "删除失败（可能不是你的瓶子/或已删除）", cancellationToken: ct);
                await bot.EditMessageReplyMarkup(chatId, callbackQuery.Message!.MessageId, replyMarkup: null, cancellationToken: ct);
                return;
            }

            // ===== 你原来的逻辑：mybottles.page =====
            if (data.StartsWith("mybottles.page:", StringComparison.OrdinalIgnoreCase))
            {
                var pageStr = data["mybottles.page:".Length..];
                if (!int.TryParse(pageStr, out var page)) page = 1;
                if (page < 1) page = 1;

                var items = await _service.ListMyBottlesAsync(userId, page, PageSize, ct);
                var markup = TelegramButtons.MyBottlesList(page, items);

                await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
                await bot.EditMessageText(
                    chatId,
                    callbackQuery.Message!.MessageId,
                    "你的瓶子：",
                    replyMarkup: markup,
                    cancellationToken: ct);
                return;
            }

            // ===== 你原来的逻辑：mybottles.view =====
            if (data.StartsWith("mybottles.view:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["mybottles.view:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                var detail = await _service.GetMyBottleDetailAsync(userId, bottleId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

                await bot.SendMessage(
                    chatId,
                    $"瓶子 {detail.bottleNo}\n\n{detail.content}\n\n被捞取次数：{detail.pickupCount}",
                    replyMarkup: TelegramButtons.DeleteMyBottle(bottleId),
                    cancellationToken: ct);

                return;
            }
        }
        catch (Exception ex)
        {
            await bot.AnswerCallbackQuery(callbackQuery.Id, ex.Message, cancellationToken: ct);
        }
    }
}