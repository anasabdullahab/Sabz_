namespace SABZ.Application.Interfaces;

/// <summary>Outcome of local image quality validation (before any AI call).</summary>
public sealed record ImageValidationResult(
    bool IsValid,
    string? Error,
    int Width = 0,
    int Height = 0,
    string? Format = null,
    bool PossiblyBlurry = false)
{
    public static ImageValidationResult Invalid(string error) => new(false, error);
}

/// <summary>
/// Validates uploaded images locally (size, type, content, dimensions,
/// corruption, blur) before they are sent to an external AI provider.
/// Never trusts the file extension alone.
/// </summary>
public interface IImageValidator
{
    ImageValidationResult Validate(byte[] imageBytes, string? contentType, string? fileName);
}
