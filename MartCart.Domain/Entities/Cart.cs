namespace MartCart.Domain.Entities;

public sealed class Cart
{
    private readonly List<CartItem> _items = new();

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MartId { get; init; }
    public decimal Threshold { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? Memo { get; set; }

    public IReadOnlyList<CartItem> Items => _items;
    public bool IsClosed => ClosedAt is not null;

    public decimal OriginalSubtotal => _items.Sum(i => i.OriginalLineTotal);
    public decimal SaleSubtotal => _items.Sum(i => i.SaleLineTotal);
    public decimal TotalSaved => _items.Sum(i => i.SavedLineTotal);

    public decimal Remaining => Math.Max(0m, Threshold - OriginalSubtotal);
    public bool IsThresholdReached => OriginalSubtotal >= Threshold;

    public void AddItem(CartItem item)
    {
        GuardOpen();
        item.CartId = Id;
        _items.Add(item);
    }

    public bool RemoveItem(Guid itemId)
    {
        GuardOpen();
        var idx = _items.FindIndex(i => i.Id == itemId);
        if (idx < 0) return false;
        _items.RemoveAt(idx);
        return true;
    }

    public void SetQuantity(Guid itemId, int qty)
    {
        GuardOpen();
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty));
        var item = _items.FirstOrDefault(i => i.Id == itemId)
                   ?? throw new InvalidOperationException("Item not found.");
        if (qty == 0) { _items.Remove(item); return; }
        item.Quantity = qty;
    }

    public void Close(DateTimeOffset? at = null)
    {
        if (IsClosed) throw new InvalidOperationException("Cart already closed.");
        ClosedAt = at ?? DateTimeOffset.UtcNow;
    }

    private void GuardOpen()
    {
        if (IsClosed) throw new InvalidOperationException("Cart is closed; modifications are not allowed.");
    }
}
