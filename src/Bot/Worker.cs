using Bot.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITelegramBotClient _bot;
    private readonly IReadOnlyList<ITelegramCommand> _messageCommands;
    private readonly IReadOnlyList<ITelegramCallbackCommand> _callbackCommands;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration config,
        IEnumerable<ITelegramCommand> messageCommands,
        IEnumerable<ITelegramCallbackCommand> callbackCommands)
    {
        _logger = logger;
        _messageCommands = messageCommands.ToList();
        _callbackCommands = callbackCommands.ToList();

        var token = config["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Missing Telegram:BotToken in configuration (User Secrets).");

        _bot = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("Telegram bot started: @{Username} (Id={Id})", me.Username, me.Id);

        await _bot.DeleteWebhook(dropPendingUpdates: true, cancellationToken: stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    if (update.Message is { } msg)
                        await HandleMessageAsync(bot, msg, ct);
                    break;

                case UpdateType.CallbackQuery:
                    if (update.CallbackQuery is { } cq)
                        await HandleCallbackAsync(bot, cq, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleUpdateAsync failed (updateType={UpdateType})", update.Type);
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.Text is null) return;

        _logger.LogInformation("Received message: {Text} (chatId={ChatId})", message.Text, message.Chat.Id);

        var cmd = _messageCommands.FirstOrDefault(c => c.CanHandle(message));
        if (cmd is not null)
        {
            await cmd.HandleAsync(bot, message, ct);
            return;
        }

        await bot.SendMessage(message.Chat.Id, "收到：" + message.Text, cancellationToken: ct);
    }

    private async Task HandleCallbackAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken ct)
    {
        _logger.LogInformation("Received callback: {Data} (from={UserId})",
            callbackQuery.Data,
            callbackQuery.From.Id);

        var cmd = _callbackCommands.FirstOrDefault(c => c.CanHandle(callbackQuery));
        if (cmd is not null)
        {
            await cmd.HandleAsync(bot, callbackQuery, ct);
            return;
        }

        // 没匹配到就先把 loading 状态???掉
        if (callbackQuery.Id is not null)
            await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}