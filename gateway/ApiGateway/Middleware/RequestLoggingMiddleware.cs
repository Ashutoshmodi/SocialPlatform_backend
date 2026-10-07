using System.Diagnostics;

namespace ApiGateway.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _requestDelegate;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate requestDelegate, ILogger<RequestLoggingMiddleware> logger)
        {
            _requestDelegate = requestDelegate;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            context.Items["RequestId"] = requestId;

            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("HTTP {Method} {Path} {RequestId} - Request started", context.Request.Method, context.Request.Path, requestId);
            await _requestDelegate(context);
            stopwatch.Stop();

            _logger.LogInformation(
            "HTTP {Method} {Path} {StatusCode} {ElapsedMs}ms {RequestId} - Request completed",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            requestId);
        }
    }
}
