namespace MartCart.Domain.Entities;

public sealed class Mart
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal DefaultThreshold { get; set; }
    public decimal DefaultDiscountAmount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
