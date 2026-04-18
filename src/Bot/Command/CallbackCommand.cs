using Application.Bottles;
using Application.Conversations;
using Bot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class CallbackCommand : ITelegramCallbackCommand
{
    private readonly BottleService _service;
    private readonly ConversationService _conversations;
    private const int PageSize = 5;

    public CallbackCommand(BottleService service, ConversationService conversations)
    {
        _service = service;
        _conversations = conversations;
    }

    public bool CanHandle(CallbackQuery callbackQuery)
        => callbackQuery.Data == "noop"
        || callbackQuery.Data == "draft.publish"
        || callbackQuery.Data == "reply.send"
        || callbackQuery.Data == "reply.cancel"
        || callbackQuery.Data?.StartsWith("todo.report:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("todo.block:", StringComparison.OrdinalIgnoreCase) == true
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
            // ===== 通用 noop =====
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
                    replyMarkup: TelegramButtons.ReplySendCancel(),
                    cancellationToken: ct);

                return;
            }
            // ===== M3未实现：举报/拉黑 =====
            if (data.StartsWith("todo.report:", StringComparison.OrdinalIgnoreCase)
                || data.StartsWith("todo.block:", StringComparison.OrdinalIgnoreCase))
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
                    replyMarkup: TelegramButtons.ReplySendCancel(),
                    cancellationToken: ct);

                return;
            }

            // ===== M3.2：发送匿名回复 =====
            if (data == "reply.send")
            {
                var (toUserId, bottleNo, content, threadId) = await _conversations.SendAsync(userId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, "已发送", cancellationToken: ct);

                // 给对方推送匿名消息（重点：不暴露任何身份信息）
                await bot.SendMessage(
                    toUserId,
                    $"收到一条匿名回复（瓶子 {bottleNo}）：\n\n{content}",
                    replyMarkup: TelegramButtons.ContinueThread(threadId),
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