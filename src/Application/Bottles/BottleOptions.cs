namespace Application.Bottles;

public sealed class BottleOptions
{
    // 默认 90，避免没配就炸
    public int ExpireDays { get; set; } = 90;
}