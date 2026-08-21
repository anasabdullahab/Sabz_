using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SABZ.Application.Interfaces;

namespace SABZ.Infrastructure.Services.DiseaseDetection;

/// <summary>
/// Local image quality validation performed BEFORE any AI provider call.
/// Content is validated via magic bytes + full decode - the file extension
/// and content-type header alone are never trusted. Malformed uploads are
/// rejected gracefully, never allowed to crash the pipeline.
/// </summary>
public class SharpImageValidator : IImageValidator
{
    private const int BlurDownscaleSize = 256;

    private readonly DiseaseDetectionSettings _settings;

    public SharpImageValidator(IOptions<DiseaseDetectionSettings> settings)
    {
        _settings = settings.Value;
    }

    public ImageValidationResult Validate(byte[] imageBytes, string? contentType, string? fileName)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return ImageValidationResult.Invalid("The uploaded image file is empty.");

        var maxBytes = (long)_settings.MaxImageSizeMb * 1024 * 1024;
        if (imageBytes.Length > maxBytes)
            return ImageValidationResult.Invalid(
                $"The uploaded image exceeds the maximum allowed size of {_settings.MaxImageSizeMb} MB.");

        var detectedType = DetectTypeFromMagicBytes(imageBytes);
        if (detectedType is null)
            return ImageValidationResult.Invalid(
                "The uploaded file does not appear to be a supported image. Supported formats: JPEG, PNG, WebP.");

        if (!_settings.AllowedImageTypes.Contains(detectedType, StringComparer.OrdinalIgnoreCase))
            return ImageValidationResult.Invalid(
                $"The image type '{detectedType}' is not supported. Supported formats: {string.Join(", ", _settings.AllowedImageTypes)}.");

        // Extension must not contradict the actual content (never trust the extension alone).
        var extension = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
        if (!string.IsNullOrEmpty(extension))
        {
            var extensionAllowed = (detectedType, extension) switch
            {
                ("image/jpeg", "jpg" or "jpeg") => true,
                ("image/png", "png") => true,
                ("image/webp", "webp") => true,
                _ => false
            };
            if (!extensionAllowed)
                return ImageValidationResult.Invalid(
                    $"The file extension '.{extension}' does not match the actual image content ({detectedType}).");
        }

        // Full decode proves the image is not corrupt/truncated and yields dimensions.
        try
        {
            using var image = Image.Load(imageBytes);

            if (image.Width < _settings.MinImageWidth || image.Height < _settings.MinImageHeight)
                return ImageValidationResult.Invalid(
                    $"The image is too small ({image.Width}x{image.Height}). " +
                    $"Please upload an image of at least {_settings.MinImageWidth}x{_settings.MinImageHeight} pixels.");

            if (image.Width > _settings.MaxImageWidth || image.Height > _settings.MaxImageHeight)
                return ImageValidationResult.Invalid(
                    $"The image dimensions ({image.Width}x{image.Height}) exceed the maximum of " +
                    $"{_settings.MaxImageWidth}x{_settings.MaxImageHeight} pixels.");

            var format = image.Metadata.DecodedImageFormat?.Name ?? "Unknown";
            var blurry = IsPossiblyBlurry(image);

            return new ImageValidationResult(true, null, image.Width, image.Height, format, blurry);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or ImageFormatException or NotSupportedException or OutOfMemoryException)
        {
            return ImageValidationResult.Invalid(
                "The uploaded file appears to be corrupt or is not a readable image. Please upload a valid photograph.");
        }
    }

    /// <summary>Magic-byte sniffing: JPEG, PNG, WebP.</summary>
    private static string? DetectTypeFromMagicBytes(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "image/webp";

        return null;
    }

    /// <summary>
    /// Cheap blur heuristic: Laplacian variance on a small grayscale copy.
    /// Low variance indicates little edge detail. Result is reported to the
    /// farmer as guidance only - never a hard rejection.
    /// </summary>
    private bool IsPossiblyBlurry(Image image)
    {
        try
        {
            using var small = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(BlurDownscaleSize, BlurDownscaleSize)
            }));

            using var gray = small.CloneAs<L8>();

            var width = gray.Width;
            var height = gray.Height;
            if (width < 3 || height < 3)
                return false;

            var rows = new byte[height][];
            gray.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                    rows[y] = Array.ConvertAll(accessor.GetRowSpan(y).ToArray(), pixel => pixel.PackedValue);
            });

            double sum = 0;
            double sumSq = 0;
            long count = 0;

            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var laplacian = 4 * rows[y][x] - rows[y][x - 1] - rows[y][x + 1] - rows[y - 1][x] - rows[y + 1][x];
                    sum += laplacian;
                    sumSq += (double)laplacian * laplacian;
                    count++;
                }
            }

            if (count == 0)
                return false;

            var mean = sum / count;
            var variance = sumSq / count - mean * mean;
            return variance < _settings.BlurVarianceThreshold;
        }
        catch
        {
            // Blur detection is best-effort; never fail the pipeline over it.
            return false;
        }
    }
}
