using MartCart.Domain.Services;

namespace MartCart.Tests.Domain;

public class HeuristicPriceClassifierTests
{
    private readonly HeuristicPriceClassifier _c = new();

    [Fact]
    public void Empty_Candidates_Returns_Zero_Confidence_NeedsConfirmation()
    {
        var r = _c.Classify(Array.Empty<decimal>());
        Assert.Equal(0.0, r.Confidence);
        Assert.True(r.RequiresUserConfirmation);
    }

    [Fact]
    public void Single_Price_Treated_As_Sale_With_No_Discount()
    {
        var r = _c.Classify(new[] { 12_800m });
        Assert.Equal(12_800m, r.Prices.OriginalPrice);
        Assert.Equal(0m, r.Prices.DiscountAmount);
        Assert.Equal(12_800m, r.Prices.SalePrice);
        Assert.Equal(0.70, r.Confidence);
    }

    [Fact]
    public void Two_Prices_Map_Max_To_Original_Min_To_Sale()
    {
        var r = _c.Classify(new[] { 15_000m, 12_000m });
        Assert.Equal(15_000m, r.Prices.OriginalPrice);
        Assert.Equal(3_000m, r.Prices.DiscountAmount);
        Assert.Equal(12_000m, r.Prices.SalePrice);
        Assert.True(r.RequiresUserConfirmation);
    }

    [Fact]
    public void Three_Prices_Passing_Validation_Get_High_Confidence()
    {
        // 정상가 15,000 − 할인 3,000 = 판매가 12,000
        var r = _c.Classify(new[] { 15_000m, 12_000m, 3_000m });
        Assert.Equal(15_000m, r.Prices.OriginalPrice);
        Assert.Equal(3_000m, r.Prices.DiscountAmount);
        Assert.Equal(12_000m, r.Prices.SalePrice);
        Assert.Equal(0.80, r.Confidence);
        Assert.False(r.RequiresUserConfirmation);
    }

    [Fact]
    public void Label_Hit_Boosts_Confidence_To_095()
    {
        var r = _c.Classify(
            new[] { 15_000m, 12_000m, 3_000m },
            new[] { "정상가", "판매가", "할인" });
        Assert.Equal(0.95, r.Confidence);
    }

    [Fact]
    public void Validation_Tolerance_Allows_10_Won()
    {
        // 15,000 − 2,995 = 12,005 (diff 5원, 통과해야 함)
        var r = _c.Classify(new[] { 15_000m, 12_005m, 2_995m });
        Assert.Equal(0.80, r.Confidence);
        Assert.False(r.RequiresUserConfirmation);
    }

    [Fact]
    public void Below_100_Won_Tokens_Are_Filtered()
    {
        // 50원은 노이즈로 간주되어 제외되므로 후보가 1개로 줄어듦
        var r = _c.Classify(new[] { 9_900m, 50m });
        Assert.Equal(9_900m, r.Prices.SalePrice);
        Assert.Equal(0.70, r.Confidence);
    }

    [Fact]
    public void Three_Prices_Failing_Validation_Requires_Confirmation()
    {
        var r = _c.Classify(new[] { 15_000m, 12_000m, 7_777m });
        Assert.True(r.RequiresUserConfirmation);
        Assert.True(r.Confidence < 0.5);
    }
}
