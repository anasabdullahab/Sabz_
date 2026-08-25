using System.Net;
using System.Text.Json;
using SABZ.Domain.Exceptions;

namespace SABZ.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        object errorResponse;

        switch (exception)
        {
            case ValidationException validationException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse = new
                {
                    message = validationException.Message,
                    errors = validationException.Errors
                };
                break;

            case ConflictException conflictException:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse = new { message = conflictException.Message };
                break;

            case AuthenticationException authException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse = new { message = authException.Message };
                break;

            case ForbiddenException forbiddenException:
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                errorResponse = new { message = forbiddenException.Message };
                break;

            case NotFoundException notFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse = new { message = notFoundException.Message };
                break;

            case WeatherProviderException weatherException:
                _logger.LogWarning(weatherException, "Weather provider error.");
                response.StatusCode = (int)HttpStatusCode.BadGateway;
                errorResponse = new { message = weatherException.Message };
                break;

            case DiseaseProviderException diseaseException:
                _logger.LogWarning(diseaseException, "Disease detection provider error.");
                response.StatusCode = (int)HttpStatusCode.BadGateway;
                errorResponse = new { message = diseaseException.Message };
                break;

            case AgronomistProviderException agronomistException:
                _logger.LogWarning(agronomistException, "Agronomist AI/speech provider error.");
                response.StatusCode = (int)HttpStatusCode.BadGateway;
                errorResponse = new { message = agronomistException.Message };
                break;

            default:
                _logger.LogError(exception, "An unexpected error occurred.");
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse = new { message = "An unexpected error occurred. Please try again later." };
                break;
        }

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(json);
    }
}
