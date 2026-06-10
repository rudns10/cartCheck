using MartCart.Domain.Contracts;
using System.Text.RegularExpressions;

namespace MartCart.Domain.Services;

/// <summary>
/// OCR 원문에서 가격 후보·상품명을 뽑는 §9.2 / §17.3 후처리 로직.
/// 플랫폼 OCR 어댑터에서 공통 사용.
/// </summary>
public static class OcrPostProcess
{
    // §9.2 가격 매칭 + 제외 가드
    private static readonly Regex PriceRe = new(
        @"(?:^|\D)(\d{1,3}(?:[,\s]\d{3})+|\d{3,7})\s*원?",
        RegexOptions.Compiled);

    private static readonly Regex BarcodeRe = new(@"(?<!\d)\d{8,14}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex DateRe = new(@"(?:19|20)\d{6}\b", RegexOptions.Compiled);
    private static readonly Regex UnitPriceRe = new(
        @"\d+\s?(?:g|ml|kg|개|ea)당\s*\d+\s*원?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YearOnlyRe = new(@"\b(?:19|20)\d{2}\b", RegexOptions.Compiled);

    public static IReadOnlyList<decimal> ExtractPrices(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<decimal>();

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var results = new List<decimal>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var hasGuard = BarcodeRe.IsMatch(line) || DateRe.IsMatch(line) || UnitPriceRe.IsMatch(line);
            var isYearOnly = YearOnlyRe.IsMatch(line) && line.IndexOf(',') < 0 && !line.Contains('원');
            if (hasGuard || isYearOnly) continue;

            foreach (Match m in PriceRe.Matches(line))
            {
                var cleaned = m.Groups[1].Value.Replace(",", "").Replace(" ", "");
                if (decimal.TryParse(cleaned, out var num) && num >= 100 && num < 10_000_000)
                    results.Add(num);
            }
        }
        return results;
    }

    /// <summary>
    /// 상품명에서 카드 결제 안내·할인 라벨·가격 라벨 등 노이즈 토큰 제거.
    /// 예: "시크릿쥬쥬 DIY툴박스 삼성카드 결제할인" → "시크릿쥬쥬 DIY툴박스"
    /// </summary>
    public static string CleanProductName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var tokens = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var kept = tokens.Where(t => !IsNoiseToken(t)).ToArray();
        return string.Join(" ", kept).Trim();
    }

    private static readonly HashSet<string> StandaloneLabels = new(StringComparer.Ordinal)
    {
        "정상가","판매가","할인가","회원가","행사가","결제가","즉시할인가","트레이더스가",
        "정상","판매","할인","행사","결제","회원","쿠폰","적립","혜택",
    };

    private static readonly Regex NoiseSuffixRe = new(
        @"^.{0,4}(카드|할인|쿠폰|결제|행사|이상|혜택|마일리지|적립)$",
        RegexOptions.Compiled);

    private static bool IsNoiseToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;
        if (StandaloneLabels.Contains(token)) return true;
        if (NoiseSuffixRe.IsMatch(token)) return true;
        return false;
    }

    /// <summary>
    /// Bounding box 정보를 이용해 "맨 왼쪽 위 큰 글자" 우선으로 상품명을 추출.
    /// 가격표는 보통 위쪽에 상품명, 아래쪽·우측에 가격이 배치된다.
    /// </summary>
    public static string? ExtractProductName(IReadOnlyList<OcrLine> lines)
    {
        if (lines is null || lines.Count == 0) return null;

        // 이미지 좌표계의 상대적 위치 계산 (이미지 크기를 모르므로 라인 분포로 정규화)
        var minY = lines.Min(l => l.BoundingBox.Y);
        var maxY = lines.Max(l => l.BoundingBox.Y + l.BoundingBox.Height);
        var range = Math.Max(1, maxY - minY);

        // 상단 60% 영역의 라인만 후보로
        var topThreshold = minY + (int)(range * 0.6);

        OcrLine? best = null;
        int bestScore = int.MinValue;

        foreach (var line in lines)
        {
            var text = (line.Text ?? "").Trim();
            if (text.Length < 2) continue;

            // 가격·날짜·바코드·단위가격 라인은 제외
            if (BarcodeRe.IsMatch(text)) continue;
            if (DateRe.IsMatch(text)) continue;
            if (UnitPriceRe.IsMatch(text)) continue;
            if (PriceRe.IsMatch(text) && text.Contains('원')) continue;

            var cleaned = new string(text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray()).Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            if (cleaned.Length < 2) continue;

            int hangul = cleaned.Count(c => c >= '가' && c <= '힣');
            int letters = cleaned.Count(char.IsLetter);
            int digits = cleaned.Count(char.IsDigit);

            // 글자가 없거나 숫자가 더 많으면 제외
            if (letters < 1) continue;
            if (digits > letters) continue;

            // 점수: 글자 크기(높이) × 5 + 한글 가중치 + 영문 - 숫자 페널티
            // 좌측 배치 약간 가산 (X 작을수록 ↑), 상단 영역에 큰 가산
            int score = line.BoundingBox.Height * 5
                        + hangul * 4
                        + letters
                        - digits * 3
                        - line.BoundingBox.X / 20;

            // 상단 영역에 있으면 큰 보너스
            if (line.BoundingBox.Y <= topThreshold) score += 200;

            if (score > bestScore)
            {
                bestScore = score;
                best = line;
            }
        }

        if (best is null) return null;
        var cleanedBest = new string(best.Text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray()).Trim();
        cleanedBest = Regex.Replace(cleanedBest, @"\s+", " ");
        cleanedBest = CleanProductName(cleanedBest);
        return string.IsNullOrEmpty(cleanedBest) ? null : cleanedBest;
    }

    /// <summary>
    /// (Legacy) bounding box 없이 텍스트만으로 상품명 추출. 위 오버로드를 우선 사용할 것.
    /// </summary>
    public static string? ExtractProductName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string? best = null;
        int bestScore = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length < 2) continue;

            // 가격·날짜·바코드 라인 스킵
            if (PriceRe.IsMatch(line) && line.Contains('원')) continue;
            if (BarcodeRe.IsMatch(line)) continue;
            if (DateRe.IsMatch(line)) continue;
            if (UnitPriceRe.IsMatch(line)) continue;

            var cleaned = new string(line.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray()).Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            if (cleaned.Length < 3) continue;

            // 한글 글자 수 + 영문 글자 수 측정
            int hangul = cleaned.Count(c => c >= '가' && c <= '힣');
            int latin = cleaned.Count(char.IsLetter) - hangul;
            int digits = cleaned.Count(char.IsDigit);
            int letters = hangul + latin;

            // 글자가 거의 없거나 숫자 비율 높으면 스킵
            if (letters < 2) continue;
            if (digits > letters) continue;

            // 점수: 한글 우선, 길이 가산, 숫자 페널티
            int score = hangul * 3 + latin + Math.Min(cleaned.Length, 30) - digits * 2;
            if (score > bestScore)
            {
                bestScore = score;
                best = cleaned;
            }
        }

        // 최소 점수 미만이면 신뢰할 수 없음
        if (bestScore < 8 || best is null) return null;
        var cleanedName = CleanProductName(best);
        return string.IsNullOrWhiteSpace(cleanedName) ? null : cleanedName;
    }
}
