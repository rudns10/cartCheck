namespace MartCart.Domain.Services;

public readonly record struct PriceTriple(decimal OriginalPrice, decimal DiscountAmount, decimal SalePrice);

public sealed record PriceClassification(
    PriceTriple Prices,
    double Confidence,
    string Source,
    bool RequiresUserConfirmation);

public interface IPriceClassifier
{
    PriceClassification Classify(IReadOnlyList<decimal> candidates, IReadOnlyList<string>? labels = null);
}
