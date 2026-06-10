namespace MartCart.Domain.Entities;

public sealed class CartItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CartId { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public NameSource NameSource { get; set; } = NameSource.Manual;

    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SalePrice { get; set; }
    public int Quantity { get; set; } = 1;

    public ItemSource Source { get; set; } = ItemSource.Manual;
    public double? OcrConfidence { get; set; }
    public string? PhotoPath { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public decimal OriginalLineTotal => OriginalPrice * Quantity;
    public decimal SaleLineTotal => SalePrice * Quantity;
    public decimal SavedLineTotal => DiscountAmount * Quantity;
}
