using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AiContentPlatform.Api.Services;

public class AiProviderExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<AiProviderExceptionHandler> _logger;

    public AiProviderExceptionHandler(IProblemDetailsService problemDetails, ILogger<AiProviderExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AiProviderException aiException)
        {
            return false;
        }

        _logger.LogWarning(aiException, "AI provider {Provider} failed", aiException.Provider);

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "The AI provider request failed.",
                Detail = aiException.Message
            }
        });
    }
}
