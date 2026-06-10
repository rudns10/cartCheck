namespace MartCart.Domain.Contracts;

/// <summary>
/// 이미지 1장에서 가격 후보·상품명 후보를 추출하는 플랫폼 OCR 어댑터.
/// </summary>
public interface IPriceOcr
{
    /// <summary>OCR을 실행하여 인식된 텍스트 라인과 가격 후보를 반환.</summary>
    Task<OcrResult> RecognizeAsync(Stream image, CancellationToken ct = default);

    /// <summary>이 플랫폼에서 한글 인식이 가능한지 (Android: 가능, iOS: 라틴만).</summary>
    bool SupportsKorean { get; }
}

public sealed record OcrResult(
    string FullText,
    IReadOnlyList<OcrLine> Lines,
    IReadOnlyList<decimal> PriceCandidates,
    string? ProductName);

public sealed record OcrLine(string Text, float Confidence, OcrBoundingBox BoundingBox);

public readonly record struct OcrBoundingBox(int X, int Y, int Width, int Height);
