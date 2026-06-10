using System.Text.Json;

namespace MartCart.AppFaithful.Services;

public sealed record CompletedCartItem(
    string Name,
    decimal OriginalPrice,
    decimal DiscountAmount,
    decimal SalePrice,
    int Quantity)
{
    public decimal SaleLineTotal => SalePrice * Quantity;
    public decimal OriginalLineTotal => OriginalPrice * Quantity;
    public decimal SavedLineTotal => DiscountAmount * Quantity;
}

public sealed record CompletedCart(
    string Id,
    string Mart,
    decimal Original,
    decimal Sale,
    decimal Saved,
    decimal Threshold,
    int ItemCount,
    bool ThresholdReached,
    DateTimeOffset ClosedAt,
    IReadOnlyList<CompletedCartItem> Items);

/// <summary>
/// Preferences 기반 장바구니 기록 보관소. v1 mockup용.
/// 추후 SQLite로 교체 가능.
/// </summary>
public static class CartHistory
{
    private const string Key = "martcart.history";

    public static IReadOnlyList<CompletedCart> All()
    {
        var json = Preferences.Default.Get(Key, "");
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<CompletedCart>();
        try
        {
            return JsonSerializer.Deserialize<List<CompletedCart>>(json) ?? new();
        }
        catch
        {
            return Array.Empty<CompletedCart>();
        }
    }

    public static CompletedCart? Find(string id)
        => All().FirstOrDefault(c => c.Id == id);

    public static void Add(CompletedCart entry)
    {
        var list = new List<CompletedCart>(All()) { entry };
        list = list.OrderByDescending(c => c.ClosedAt).ToList();
        Preferences.Default.Set(Key, JsonSerializer.Serialize(list));
    }

    public static void Clear() => Preferences.Default.Remove(Key);

    public static void Delete(string id)
    {
        var list = new List<CompletedCart>(All());
        list.RemoveAll(c => c.Id == id);
        Preferences.Default.Set(Key, JsonSerializer.Serialize(list));
    }
}
