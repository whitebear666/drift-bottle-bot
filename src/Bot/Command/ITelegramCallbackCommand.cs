using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public interface ITelegramCallbackCommand
{
    bool CanHandle(CallbackQuery callbackQuery);
    Task HandleAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken ct);
}