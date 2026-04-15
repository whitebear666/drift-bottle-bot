using Bot.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;

namespace Bot;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITelegramBotClient _bot;
    private readonly IReadOnlyList<ITelegramCommand> _commands;
    public Worker(ILogger<Worker> logger, IConfiguration config, IEnumerable<ITelegramCommand> commands)
    {
        _logger = logger;
        _commands = commands.ToList();

        var token = config["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Missing Telegram:BotToken in configuration (User Secrets).");

        _bot = new TelegramBotClient(token);
    }
    /*public Worker(ILogger<Worker> logger, IEnumerable<ITelegramCommand> commands)
    {
        _logger = logger;
        _commands = commands.ToList();

        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Missing TELEGRAM_BOT_TOKEN environment variable.");

        _bot = new TelegramBotClient(token);
    }*///这个是将token放到launchSetting中了，不安全，不用它

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("Telegram bot started: @{Username} (Id={Id})", me.Username, me.Id);

        // 如果之前设置过 webhook，会导致 long polling 收不到更新；这里直接清掉
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
        if (update.Type != UpdateType.Message) return;

        var message = update.Message;
        if (message?.Text is null) return;

        var chatId = message.Chat.Id;
        var text = message.Text;

        _logger.LogInformation("Received: {Text} (chatId={ChatId})", text, chatId);

        var cmd = _commands.FirstOrDefault(c => c.CanHandle(message));
        if (cmd is not null)
        {
            await cmd.HandleAsync(bot, message, ct);
            return;
        }

        // 默认回显（后续可以改成“未知命令/提示help”）
        await bot.SendMessage(chatId, "收到：" + text, cancellationToken: ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}