using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Telegram;

public static class TelegramButtons
{
    // callback data formats
    // bottle.delete:{bottleId}
    // bottle.reply:{bottleId}
    // thread.reply:{threadId}
    // reply.send
    // reply.cancel
    // mybottles.page:{page}
    // mybottles.view:{bottleId}

    public static InlineKeyboardMarkup DeleteMyBottle(Guid bottleId)
        => new(InlineKeyboardButton.WithCallbackData("删除我的瓶子", $"bottle.delete:{bottleId:D}"));

    public static InlineKeyboardMarkup PublishDraft()
        => new(InlineKeyboardButton.WithCallbackData("结束编辑并发布瓶子", "draft.publish"));

    public static InlineKeyboardButton Noop(string text)
        => InlineKeyboardButton.WithCallbackData(text, "noop");

    public static InlineKeyboardMarkup PickedBottleActions(Guid bottleId)
        => new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("举报(未实现)", $"todo.report:{bottleId:D}") },
            // 入口：只从捞到的瓶子进入
            new[] { InlineKeyboardButton.WithCallbackData("回复", $"bottle.reply:{bottleId:D}") },
            new[] { InlineKeyboardButton.WithCallbackData("拉黑(未实现)", $"todo.block:{bottleId:D}") },
        });

    public static InlineKeyboardMarkup MyBottlesPage(int page)
    {
        var prev = page <= 1
            ? Noop("上一页")
            : InlineKeyboardButton.WithCallbackData("上一页", $"mybottles.page:{page - 1}");

        var next = InlineKeyboardButton.WithCallbackData("下一页", $"mybottles.page:{page + 1}");

        return new InlineKeyboardMarkup(new[]
        {
            new[] { prev, next }
        });
    }

    public static InlineKeyboardMarkup MyBottlesList(int page, IReadOnlyList<(Guid id, string bottleNo)> items)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var it in items)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(it.bottleNo, $"mybottles.view:{it.id:D}") });

        var prev = page <= 1
            ? Noop("上一页")
            : InlineKeyboardButton.WithCallbackData("上一页", $"mybottles.page:{page - 1}");

        var next = InlineKeyboardButton.WithCallbackData("下一页", $"mybottles.page:{page + 1}");

        rows.Add(new[] { prev, next });

        return new InlineKeyboardMarkup(rows);
    }

    // ===== M3.2：匿名对话 =====

    public static InlineKeyboardMarkup ContinueThread(Guid threadId)
        => new(InlineKeyboardButton.WithCallbackData("继续回复", $"thread.reply:{threadId:D}"));

    public static InlineKeyboardMarkup ReplySendCancel()
        => new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("发送", "reply.send"),
                InlineKeyboardButton.WithCallbackData("取消", "reply.cancel")
            }
        });
}