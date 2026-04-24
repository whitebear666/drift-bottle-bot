using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Telegram;

public static class TelegramButtons
{
    // callback data formats
    //
    // bottle.delete:{bottleId}
    // bottle.reply:{bottleId}
    // bottle.report.todo:{bottleId}         // TODO：举报瓶子
    // bottle.block.todo:{bottleId}          // TODO：拉黑/屏蔽瓶子（语义后续定）
    //
    // thread.reply:{threadId}
    // thread.report.todo:{threadId}         // TODO：举报会话
    // thread.block.confirm:{threadId}       // 拉黑二次确认（thread 级）
    // thread.block:{threadId}               // 执行拉黑本会话（thread 级）
    // user.block.permanent.confirm:{threadId} // TODO：永久拉黑（将来建议改成 userId）
    //
    // reply.send
    // reply.cancel
    //
    // mybottles.page:{page}
    // mybottles.view:{bottleId}

    public static InlineKeyboardMarkup DeleteMyBottle(Guid bottleId)
        => new(InlineKeyboardButton.WithCallbackData("删除我的瓶子", $"bottle.delete:{bottleId:D}"));

    public static InlineKeyboardMarkup PublishDraft()
        => new(InlineKeyboardButton.WithCallbackData("结束编辑并发布瓶子", "draft.publish"));

    public static InlineKeyboardButton Noop(string text)
        => InlineKeyboardButton.WithCallbackData(text, "noop");

    //这个是捞到瓶子的时候的
    public static InlineKeyboardMarkup PickedBottleActions(Guid bottleId)
        => new(new[]
        {
            //new[] { InlineKeyboardButton.WithCallbackData("举报(未实现)", $"bottle.report.todo:{bottleId:D}") },
            //上面是未实现，下面是已实现，拉黑同理哦
            new[] { InlineKeyboardButton.WithCallbackData("举报(未实现)", $"bottle.report:{bottleId:D}") },
            // 入口：只从捞到的瓶子进入
            new[] { InlineKeyboardButton.WithCallbackData("回复", $"bottle.reply:{bottleId:D}") },
            //new[] { InlineKeyboardButton.WithCallbackData("拉黑(未实现)", $"bottle.block.todo:{bottleId:D}") },
            new[] { InlineKeyboardButton.WithCallbackData("拉黑(未实现)", $"bottle.block:{bottleId:D}") },
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
    //这个是回复的时候的
    // 收到匿名回复时的按钮：继续回复 / 举报（TODO）/ 拉黑（需要二次确认）
    public static InlineKeyboardMarkup IncomingAnonymousMessage(Guid threadId)
        => new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("继续回复", $"thread.reply:{threadId:D}") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("举报（TODO）", $"thread.report.todo:{threadId:D}"),

                // 先走确认流程，拉黑需要二次确认：先进入确认面板，不直接执行
                InlineKeyboardButton.WithCallbackData("拉黑", $"thread.block.confirm:{threadId:D}")
            }
        });

    // 拉黑确认面板：
    // 1) 同意：拉黑本会话（thread）
    // 2) 取消
    // 3) 永久拉黑（TODO，占位，后续再实现）
    public static InlineKeyboardMarkup ConfirmBlock(Guid threadId)
        => new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("同意，我已想好（拉黑本会话）", $"thread.block:{threadId:D}") },
            new[] { InlineKeyboardButton.WithCallbackData("永久拉黑此人（TODO）", $"user.block.permanent.confirm:{threadId:D}") },
            new[] { InlineKeyboardButton.WithCallbackData("取消，我再想想", "noop") }
        });

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