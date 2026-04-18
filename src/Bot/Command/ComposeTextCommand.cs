using Application.Bottles;
using Application.Conversations;
using Application.Users.Contracts;
using Bot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public sealed class ComposeTextCommand : ITelegramCommand
{
    private readonly BottleService _service;
    private readonly ConversationService _conversations;
    private readonly IUserStateRepository _userStates;

    public ComposeTextCommand(BottleService service, ConversationService conversations, IUserStateRepository userStates)
    {
        _service = service;
        _conversations = conversations;
        _userStates = userStates;
    }

    public bool CanHandle(Message message)
    {
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
            var state = await _userStates.GetAsync(userId, ct);

            // 回复模式：写入匿名回复草稿
            if (state.IsReplying)
            {
                await _conversations.AppendDraftAsync(userId, message.Text!, ct);

                await bot.SendMessage(
                    chatId,
                    "已添加到匿名回复草稿。继续输入，或点击下方按钮“发送/取消”。",
                    replyMarkup: TelegramButtons.ReplySendCancel(),
                    cancellationToken: ct);

                return;
            }

            // 普通模式：写入瓶子草稿
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