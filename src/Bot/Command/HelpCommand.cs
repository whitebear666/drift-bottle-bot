using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class HelpCommand : ITelegramCommand
{
    public bool CanHandle(Message message)
        => message.Text?.StartsWith("/help", StringComparison.OrdinalIgnoreCase) == true;

    public Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
        => bot.SendMessage(
            chatId: message.Chat.Id,
            text: "/start - 启动\n/help - 帮助",
            cancellationToken: ct);
}