using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Telegram;

public static class BotMenus
{
    public const string StartCompose = "开始发一个瓶子";
    public const string Publish = "结束编辑并发布瓶子";
    public const string Pickup = "捞一个瓶子";
    public const string MyBottles = "查看我发过的瓶子";

    public static ReplyKeyboardMarkup MainMenu()
        => new(new[]
        {
            new KeyboardButton[] { StartCompose, Publish },
            new KeyboardButton[] { Pickup, MyBottles }
        })
        {
            ResizeKeyboard = true
        };
}