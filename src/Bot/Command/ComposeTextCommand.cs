using Application.Bottles;
using Bot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class ComposeTextCommand : ITelegramCommand
{
    private readonly BottleService _service;

    public ComposeTextCommand(BottleService service)
    {
        _service = service;
    }

    public bool CanHandle(Message message)
    {
        // 只处理普通文本（排除 /start /help 等命令；菜单文字由 MenuCommand 处理）
        if (string.IsNullOrWhiteSpace(message.Text)) return false;
        if (message.Text.StartsWith("/")) return false;

        // 菜单按钮文本不在这里处理
        if (message.Text is BotMenus.StartCompose or BotMenus.Publish or BotMenus.Pickup or BotMenus.MyBottles)
            return false;

        return true;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        try
        {
            await _service.AppendDraftAsync(userId, message.Text!, ct);
            await bot.SendMessage(
                chatId,
                "已添加到草稿。继续输入，或点击下方按钮“结束编辑并发布瓶子”。",
                replyMarkup: TelegramButtons.PublishDraft(),
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, ex.Message, replyMarkup: BotMenus.MainMenu(), cancellationToken: ct);
        }
    }
}