using MartCart.Domain.Entities;

namespace MartCart.Domain.Services;

/// <summary>
/// 현재 진행 중인 장바구니의 인메모리 보관소. 앱 라이프타임 싱글톤.
/// 추후 SQLite 영속화로 교체 가능.
/// </summary>
public interface ICurrentCart
{
    Cart Cart { get; }
    event EventHandler? Changed;
    void AddItem(CartItem item);
    bool RemoveItem(Guid itemId);
    void Clear();
    void SetThreshold(decimal threshold);
    void SetDiscount(decimal discount);
    void IncrementQuantity(Guid itemId);
    void DecrementQuantity(Guid itemId);
}

public sealed class InMemoryCurrentCart : ICurrentCart
{
    public Cart Cart { get; private set; } = new Cart
    {
        MartId = Guid.NewGuid(),
        Threshold = 50_000m,
        DiscountAmount = 5_000m,
    };

    public event EventHandler? Changed;

    public void AddItem(CartItem item)
    {
        Cart.AddItem(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveItem(Guid itemId)
    {
        var ok = Cart.RemoveItem(itemId);
        if (ok) Changed?.Invoke(this, EventArgs.Empty);
        return ok;
    }

    public void SetThreshold(decimal threshold)
    {
        if (threshold < 0) throw new ArgumentOutOfRangeException(nameof(threshold));
        Cart.Threshold = threshold;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetDiscount(decimal discount)
    {
        if (discount < 0) throw new ArgumentOutOfRangeException(nameof(discount));
        Cart.DiscountAmount = discount;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void IncrementQuantity(Guid itemId)
    {
        var item = Cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return;
        Cart.SetQuantity(itemId, item.Quantity + 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void DecrementQuantity(Guid itemId)
    {
        var item = Cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return;
        // SetQuantity(0) auto-removes
        Cart.SetQuantity(itemId, Math.Max(0, item.Quantity - 1));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Cart = new Cart
        {
            MartId = Cart.MartId,
            Threshold = Cart.Threshold,
            DiscountAmount = Cart.DiscountAmount,
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
