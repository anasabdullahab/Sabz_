namespace SABZ.Domain.Exceptions;

/// <summary>
/// Thrown when the crop price data provider fails (timeout, unavailable
/// source, malformed provider response, configuration problem). Mapped to
/// HTTP 502 by the <c>GlobalExceptionMiddleware</c>; internal details never
/// leave the middleware boundary.
/// </summary>
public class CropPriceProviderException : Exception
{
    public CropPriceProviderException(string message) : base(message) { }

    public CropPriceProviderException(string message, Exception innerException)
        : base(message, innerException) { }
}
