using Application.Bottles;
using Bot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class MenuCommand : ITelegramCommand
{
    private readonly BottleService _service;

    public MenuCommand(BottleService service)
    {
        _service = service;
    }

    public bool CanHandle(Message message)
        => message.Text is BotMenus.StartCompose
        or BotMenus.Publish
        or BotMenus.Pickup
        or BotMenus.MyBottles;

    public async Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var text = message.Text!;

        try
        {
            switch (text)
            {
                case BotMenus.StartCompose:
                    await _service.StartComposeAsync(userId, ct);
                    await bot.SendMessage(
                        chatId,
                        "已进入编辑模式：请发送瓶子内容（可多条消息拼接）。发送完成后点击“结束编辑并发布瓶子”。",
                        replyMarkup: BotMenus.MainMenu(),
                        cancellationToken: ct);
                    return;

                case BotMenus.Publish:
                    {
                        // M2: PublishAsync returns quota
                        var (bottleId, bottleNo, quota) = await _service.PublishAsync(userId, ct);

                        var markup = TelegramButtons.DeleteMyBottle(bottleId);

                        await bot.SendMessage(
                            chatId,
                            $"已扔出一个瓶子：{bottleNo}\n当前可捞次数：{quota}",
                            replyMarkup: markup,
                            cancellationToken: ct);
                        return;
                    }

                case BotMenus.Pickup:
                    {
                        // M2: PickupAsync returns quota
                        var picked = await _service.PickupAsync(userId, ct);

                        var markup = TelegramButtons.PickedBottleActions(picked.bottleId);

                        await bot.SendMessage(
                            chatId,
                            $"你捞到了瓶子 {picked.bottleNo}\n\n{picked.content}\n\n被捞取次数：{picked.pickupCount}\n当前可捞次数：{picked.pickupQuota}",
                            replyMarkup: markup,
                            cancellationToken: ct);
                        return;
                    }

                case BotMenus.MyBottles:
                    {
                        var markup = TelegramButtons.MyBottlesPage(page: 1);
                        await bot.SendMessage(
                            chatId,
                            "你的瓶子：",
                            replyMarkup: markup,
                            cancellationToken: ct);
                        return;
                    }
            }
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, ex.Message, replyMarkup: BotMenus.MainMenu(), cancellationToken: ct);
        }
    }
}