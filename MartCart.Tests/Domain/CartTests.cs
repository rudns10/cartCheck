using MartCart.Domain.Entities;

namespace MartCart.Tests.Domain;

public class CartTests
{
    private static CartItem Item(decimal orig, decimal disc, decimal sale, int qty = 1) => new()
    {
        Name = "x",
        OriginalPrice = orig,
        DiscountAmount = disc,
        SalePrice = sale,
        Quantity = qty,
    };

    [Fact]
    public void Subtotals_Sum_Per_Field()
    {
        var cart = new Cart { MartId = Guid.NewGuid(), Threshold = 50_000m };
        cart.AddItem(Item(10_000m, 1_000m, 9_000m, qty: 2)); // orig 20k, sale 18k, saved 2k
        cart.AddItem(Item(5_000m, 0m, 5_000m, qty: 1));      // orig 5k,  sale 5k,  saved 0

        Assert.Equal(25_000m, cart.OriginalSubtotal);
        Assert.Equal(23_000m, cart.SaleSubtotal);
        Assert.Equal(2_000m, cart.TotalSaved);
    }

    [Fact]
    public void Threshold_Uses_OriginalSubtotal()
    {
        var cart = new Cart { MartId = Guid.NewGuid(), Threshold = 50_000m };
        // Sale 합산이 임계치를 넘어도, Original이 부족하면 미달이어야 한다.
        cart.AddItem(Item(orig: 40_000m, disc: 0m, sale: 40_000m));
        cart.AddItem(Item(orig: 9_000m, disc: 0m, sale: 9_000m));

        Assert.Equal(49_000m, cart.OriginalSubtotal);
        Assert.False(cart.IsThresholdReached);
        Assert.Equal(1_000m, cart.Remaining);
    }

    [Fact]
    public void Threshold_Reached_When_Original_GreaterOrEqual()
    {
        var cart = new Cart { MartId = Guid.NewGuid(), Threshold = 50_000m };
        cart.AddItem(Item(50_000m, 5_000m, 45_000m));

        Assert.True(cart.IsThresholdReached);
        Assert.Equal(0m, cart.Remaining);
    }

    [Fact]
    public void Closed_Cart_Rejects_Modification()
    {
        var cart = new Cart { MartId = Guid.NewGuid(), Threshold = 50_000m };
        cart.AddItem(Item(1_000m, 0m, 1_000m));
        cart.Close();

        Assert.True(cart.IsClosed);
        Assert.Throws<InvalidOperationException>(() => cart.AddItem(Item(1m, 0m, 1m)));
        Assert.Throws<InvalidOperationException>(() => cart.RemoveItem(Guid.NewGuid()));
    }

    [Fact]
    public void SetQuantity_Zero_Removes_Item()
    {
        var cart = new Cart { MartId = Guid.NewGuid(), Threshold = 50_000m };
        var i = Item(1_000m, 0m, 1_000m);
        cart.AddItem(i);

        cart.SetQuantity(i.Id, 0);
        Assert.Empty(cart.Items);
    }
}
