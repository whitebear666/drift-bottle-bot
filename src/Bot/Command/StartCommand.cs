using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class StartCommand : ITelegramCommand
{
    public bool CanHandle(Message message)
        => message.Text?.StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true;

    public Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
        => bot.SendMessage(
            chatId: message.Chat.Id,
            text: "漂流瓶 Bot 已启动。输入 /help 查看命令。",
            cancellationToken: ct);
}