using Application.Bottles;
using Bot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class CallbackCommand : ITelegramCallbackCommand
{
    private readonly BottleService _service;
    private const int PageSize = 5;

    public CallbackCommand(BottleService service)
    {
        _service = service;
    }

    public bool CanHandle(CallbackQuery callbackQuery)
        => callbackQuery.Data?.StartsWith("bottle.delete:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("mybottles.page:", StringComparison.OrdinalIgnoreCase) == true
        || callbackQuery.Data?.StartsWith("mybottles.view:", StringComparison.OrdinalIgnoreCase) == true;

    public async Task HandleAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken ct)
    {
        var data = callbackQuery.Data!;
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message!.Chat.Id;

        try
        {
            if (data.StartsWith("bottle.delete:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["bottle.delete:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                var ok = await _service.DeleteMyBottleAsync(userId, bottleId, ct);

                await bot.AnswerCallbackQuery(callbackQuery.Id, ok ? "已删除" : "删除失败（可能不是你的瓶子/或已删除）", cancellationToken: ct);

                // 更新原消息（去掉按钮）
                await bot.EditMessageReplyMarkup(chatId, callbackQuery.Message!.MessageId, replyMarkup: null, cancellationToken: ct);
                return;
            }

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

            /*if (data.StartsWith("mybottles.view:", StringComparison.OrdinalIgnoreCase))
            {
                var idStr = data["mybottles.view:".Length..];
                if (!Guid.TryParse(idStr, out var bottleId))
                    throw new InvalidOperationException("无效的瓶子编号。");

                // M1：暂时不区分“必须是我的瓶子”，但仓储会按 author list 来展示入口，所以基本不会越权。
                // 如需严格：要在 Application 增加 GetMyBottleDetail 用例校验作者。
                // 这里我们先直接展示（仅用于开发验证）。
                await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

                // 复用：先简单显示“无法获取详情”也行；正式做法要在 Application 加 GetById + 校验 author
                await bot.SendMessage(chatId, "查看详情（M1 简化版）：该功能下一步补充为“仅作者可看详情”。", cancellationToken: ct);
                return;
            }*/
        }
        catch (Exception ex)
        {
            await bot.AnswerCallbackQuery(callbackQuery.Id, ex.Message, cancellationToken: ct);
        }
    }
}