namespace SABZ.Application.DTOs.Agronomist;

/// <summary>
/// Request body for a text agronomist question (Prompt 13).
/// Deliberately minimal - only the farmer's question. The authenticated
/// user and farm come from the JWT and route, never from the body.
/// </summary>
public class TextAgronomistQuestionDto
{
    /// <summary>The agriculture-related question (English or Urdu).</summary>
    public string Message { get; set; } = string.Empty;
}
