using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UserService.Infrastructure
{
    // Put [InternalApiKey] on a controller to allow only callers that send the shared key.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class InternalApiKeyAttribute : Attribute, IAuthorizationFilter
    {
        public const string HeaderName = "X-Internal-Api-Key";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = config["InternalApi:Key"];
            var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(expected) || !FixedTimeEquals(expected, provided))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid internal API key" });
            }
        }

        // Constant-time comparison, so response timing doesn't leak how much of the key was right
        private static bool FixedTimeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}