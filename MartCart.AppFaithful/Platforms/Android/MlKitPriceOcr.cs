using Android.Graphics;
using Android.Gms.Tasks;
using MartCart.Domain.Contracts;
using MartCart.Domain.Services;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Korean;

namespace MartCart.AppFaithful.Platforms.Android;

public sealed class MlKitPriceOcr : IPriceOcr
{
    public bool SupportsKorean => true;

    public async Task<OcrResult> RecognizeAsync(Stream image, System.Threading.CancellationToken ct = default)
    {
        using var bitmap = await BitmapFactory.DecodeStreamAsync(image)
            ?? throw new InvalidOperationException("Failed to decode image.");

        var input = InputImage.FromBitmap(bitmap, 0);
        using var recognizer = TextRecognition.GetClient(new KoreanTextRecognizerOptions.Builder().Build());

        var task = recognizer.Process(input);
        var result = (Text)await task.AsAsync(ct);

        var lines = new List<OcrLine>();
        foreach (Text.TextBlock block in result.TextBlocks)
        {
            foreach (Text.Line line in block.Lines)
            {
                var rect = line.BoundingBox ?? new global::Android.Graphics.Rect();
                lines.Add(new OcrLine(
                    line.Text ?? "",
                    line.Confidence,
                    new OcrBoundingBox(rect.Left, rect.Top, rect.Width(), rect.Height())));
            }
        }

        var fullText = string.Join("\n", lines.Select(l => l.Text));
        var candidates = OcrPostProcess.ExtractPrices(fullText);
        var name = OcrPostProcess.ExtractProductName(lines)
                   ?? OcrPostProcess.ExtractProductName(fullText);

        return new OcrResult(fullText, lines, candidates, name);
    }
}

internal static class AndroidTaskExtensions
{
    public static Task<Java.Lang.Object?> AsAsync(this global::Android.Gms.Tasks.Task task, System.Threading.CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<Java.Lang.Object?>();
        ct.Register(() => tcs.TrySetCanceled());
        task.AddOnSuccessListener(new SuccessListener(tcs));
        task.AddOnFailureListener(new FailureListener(tcs));
        return tcs.Task;
    }

    private sealed class SuccessListener : Java.Lang.Object, IOnSuccessListener
    {
        private readonly TaskCompletionSource<Java.Lang.Object?> _tcs;
        public SuccessListener(TaskCompletionSource<Java.Lang.Object?> tcs) => _tcs = tcs;
        public void OnSuccess(Java.Lang.Object? value) => _tcs.TrySetResult(value);
    }

    private sealed class FailureListener : Java.Lang.Object, IOnFailureListener
    {
        private readonly TaskCompletionSource<Java.Lang.Object?> _tcs;
        public FailureListener(TaskCompletionSource<Java.Lang.Object?> tcs) => _tcs = tcs;
        public void OnFailure(Java.Lang.Exception e) => _tcs.TrySetException(new Exception(e.Message ?? "ML Kit failure", e));
    }
}
