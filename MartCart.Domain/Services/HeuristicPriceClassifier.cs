namespace MartCart.Domain.Services;

/// <summary>
/// §9.3 폴백 분류기. 검증식(정상가 − 할인 = 판매가, ±10원)을 우선 신호로 사용.
/// </summary>
public sealed class HeuristicPriceClassifier : IPriceClassifier
{
    private const decimal ValidationTolerance = 10m;

    private static readonly HashSet<string> OriginalLabels = new(StringComparer.Ordinal)
    { "정상가", "할인전", "원가", "원래가격" };
    private static readonly HashSet<string> DiscountLabels = new(StringComparer.Ordinal)
    { "할인", "세일", "적립할인", "회원할인", "행사할인", "-", "−" };
    private static readonly HashSet<string> SaleLabels = new(StringComparer.Ordinal)
    { "판매가", "행사가", "회원가", "즉시할인가", "트레이더스가", "결제가" };

    public PriceClassification Classify(IReadOnlyList<decimal> candidates, IReadOnlyList<string>? labels = null)
    {
        var filtered = candidates.Where(c => c >= 100m).Distinct().OrderByDescending(c => c).ToList();

        if (filtered.Count == 0)
            return new PriceClassification(default, 0.0, "Heuristic", true);

        if (filtered.Count == 1)
        {
            var p = filtered[0];
            return new PriceClassification(new PriceTriple(p, 0m, p), 0.70, "Heuristic", false);
        }

        if (filtered.Count == 2)
        {
            var max = filtered[0];
            var min = filtered[1];
            return new PriceClassification(
                new PriceTriple(max, max - min, min),
                0.50,
                "Heuristic",
                RequiresUserConfirmation: true);
        }

        // 3+: 검증식 매칭 조합 탐색.
        for (int i = 0; i < filtered.Count; i++)
        for (int j = 0; j < filtered.Count; j++)
        for (int k = 0; k < filtered.Count; k++)
        {
            if (i == j || j == k || i == k) continue;
            var orig = filtered[i];
            var sale = filtered[j];
            var disc = filtered[k];
            if (orig <= sale) continue;
            if (Math.Abs(orig - disc - sale) <= ValidationTolerance)
            {
                var hasLabel = labels is not null && HasLabelHit(labels);
                var conf = hasLabel ? 0.95 : 0.80;
                return new PriceClassification(
                    new PriceTriple(orig, disc, sale), conf, "Heuristic",
                    RequiresUserConfirmation: false);
            }
        }

        // 검증식 실패 — 사용자 확인 필요.
        var maxC = filtered[0];
        var minC = filtered[^1];
        return new PriceClassification(
            new PriceTriple(maxC, maxC - minC, minC),
            0.40, "Heuristic", RequiresUserConfirmation: true);
    }

    private static bool HasLabelHit(IReadOnlyList<string> labels)
    {
        foreach (var raw in labels)
        {
            var l = raw.Trim();
            if (OriginalLabels.Contains(l) || DiscountLabels.Contains(l) || SaleLabels.Contains(l))
                return true;
        }
        return false;
    }
}
