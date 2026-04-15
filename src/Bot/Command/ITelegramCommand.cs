using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public interface ITelegramCommand
{
    bool CanHandle(Message message);
    Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct);
}