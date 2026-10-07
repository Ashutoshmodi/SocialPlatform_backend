using System.Collections.Concurrent;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;

namespace ApiGateway.Middleware
{
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitMiddleware> _logger;
        private static readonly ConcurrentDictionary<string, (int count, DateTime resetTime)> _requestCounts = new();

        private const int MaxRequests = 100;
        private const int WindowSizeSeconds = 60;
        public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userId = context.User?.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(userId))
            {
                userId = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            }
            var key = $"rate-limit:{userId}";
            var now = DateTime.UtcNow;

            if (_requestCounts.TryGetValue(key, out var request))
            {
                if ((now - request.resetTime).TotalSeconds < WindowSizeSeconds)
                {
                    if (request.count >= MaxRequests)
                    {
                        _logger.LogInformation("Rate limit exceeded for { UserId}", userId);
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded" });
                        return;
                    }
                    _requestCounts[key] = (request.count + 1, request.resetTime);
                }
                else
                {
                    _requestCounts[key] = (1, now);
                }
            }
            else
            {
                _requestCounts[key] = (1, now);
            }

            // Clean up old entries
            if (now.Ticks % 1000 == 0)
            {
                foreach (var kvp in _requestCounts.ToList())
                {
                    if ((now - kvp.Value.resetTime).TotalSeconds > WindowSizeSeconds)
                    {
                        _requestCounts.TryRemove(kvp.Key, out _);
                    }
                }
            }

            await _next(context);
        }

    }
}
