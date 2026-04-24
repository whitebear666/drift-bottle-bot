using Application.Bottles.Contracts;
using Application.Risk.Contracts;
using Bot.Moderation;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

//管理员配置，这里面是管理员的配置命令
namespace Bot.Commands;

public sealed class AdminCommand : ITelegramCommand
{
    private readonly IBottleRepository _bottles;
    private readonly IBanRepository _bans;
    private readonly IReportRepository _reports;
    private readonly IUserBlockRepository _blocks;
    private readonly AdminOptions _options;

    public AdminCommand(
        IBottleRepository bottles,
        IBanRepository bans,
        IReportRepository reports,
        IUserBlockRepository blocks,
        IOptions<AdminOptions> options)
    {
        _bottles = bottles;
        _bans = bans;
        _reports = reports;
        _blocks = blocks;
        _options = options.Value;
    }

    public bool CanHandle(Message message)
        => message.Text?.StartsWith("/admin_", StringComparison.OrdinalIgnoreCase) == true;

    public async Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var fromId = message.From?.Id ?? 0;

        if (!_options.AdminUserIds.Contains(fromId))
        {
            await bot.SendMessage(chatId, "你无权使用管理员命令。", cancellationToken: ct);
            return;
        }

        var text = (message.Text ?? "").Trim();
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "/admin_unban":
                    if (parts.Length < 2 || !long.TryParse(parts[1], out var unbanUserId))
                        throw new InvalidOperationException("用法：/admin_unban <userId>");

                    await _bans.UnbanAsync(unbanUserId, ct);
                    await bot.SendMessage(chatId, $"已解除封禁：{unbanUserId}", cancellationToken: ct);
                    return;

                case "/admin_ban":
                    if (parts.Length < 3 || !long.TryParse(parts[1], out var banUserId) || !int.TryParse(parts[2], out var days))
                        throw new InvalidOperationException("用法：/admin_ban <userId> <days>");

                    var until = DateTimeOffset.UtcNow.AddDays(days);
                    await _bans.BanAsync(banUserId, until, $"管理员封禁 {days} 天", ct);
                    await bot.SendMessage(chatId, $"已封禁：{banUserId}，到期：{until:yyyy-MM-dd HH:mm} UTC", cancellationToken: ct);
                    return;

                case "/admin_bottle_no":
                    if (parts.Length < 2)
                        throw new InvalidOperationException("用法：/admin_bottle_no <bottleNo>");

                    var bno = parts[1];
                    var bottle = await _bottles.GetByNoAsync(bno, ct);
                    if (bottle is null)
                    {
                        await bot.SendMessage(chatId, "未找到该瓶子。", cancellationToken: ct);
                        return;
                    }

                    await bot.SendMessage(
                        chatId,
                        $"BottleNo: {bottle.BottleNo}\nBottleId: {bottle.Id}\nAuthor: {bottle.AuthorUserId}\nCreatedAt: {bottle.CreatedAt:yyyy-MM-dd HH:mm} UTC\nDeleted: {bottle.IsDeleted}\n\n{bottle.Content}",
                        cancellationToken: ct);

                    return;

                case "/admin_stats":
                    if (parts.Length < 2 || !long.TryParse(parts[1], out var authorId))
                        throw new InvalidOperationException("用法：/admin_stats <authorUserId>");

                    var reportCnt = await _reports.CountDistinctReportersAsync(authorId, ct);
                    var blockCnt = await _blocks.CountDistinctBlockersAsync(authorId, ct);
                    var bannedUntil = await _bans.GetBannedUntilAsync(authorId, ct);

                    var banText = (bannedUntil is { } u && u > DateTimeOffset.UtcNow)
                        ? $"封禁中，解封：{u:yyyy-MM-dd HH:mm} UTC"
                        : "未封禁";

                    await bot.SendMessage(
                        chatId,
                        $"Author: {authorId}\n被不同用户举报数：{reportCnt}\n被不同用户拉黑数：{blockCnt}\n状态：{banText}",
                        cancellationToken: ct);

                    return;

                default:
                    await bot.SendMessage(
                        chatId,
                        "支持命令：\n/admin_unban <userId>\n/admin_ban <userId> <days>\n/admin_bottle_no <bottleNo>\n/admin_stats <authorUserId>",
                        cancellationToken: ct);
                    return;
            }
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, ex.Message, cancellationToken: ct);
        }
    }
}