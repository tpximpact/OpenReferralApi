using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;

namespace OpenReferralApi.Core.Services;

public interface IAuthenticationValidationService
{
    DataSourceAuthentication? TryGetValidatedRequestAuthentication(string context, DataSourceAuthentication? auth);
}

public class AuthenticationValidationService(
    ILogger<AuthenticationValidationService> logger,
    IOptions<OpenApiValidationServerOptions> authOptions) : IAuthenticationValidationService
{
    private readonly ILogger<AuthenticationValidationService> _logger = logger;
    private readonly bool _allowUserSuppliedAuth = authOptions.Value.AllowUserSuppliedAuth;

    public DataSourceAuthentication? TryGetValidatedRequestAuthentication(string context, DataSourceAuthentication? auth)
    {
        if (!_allowUserSuppliedAuth)
        {
            if (auth != null)
            {
                _logger.AuthenticationFailed("User-supplied authentication was provided for context '" + TextSanitizer.SanitizeForLogging(context) + "' but is disabled by server configuration");
            }
            return null;
        }

        var validated = ValidateAuthentication(auth);
        if (validated == null && auth != null)
        {
            _logger.AuthenticationFailed("Rejected invalid user-supplied authentication for context '" + TextSanitizer.SanitizeForLogging(context) + "'");
        }
        return validated;
    }

    private static DataSourceAuthentication? ValidateAuthentication(DataSourceAuthentication? auth)
    {
        if (auth == null)
        {
            return null;
        }

        const int maxTokenLength = 4096;

        static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        static bool IsTooLong(string? value, int maxLength) => !string.IsNullOrEmpty(value) && value.Length > maxLength;

        var apiKey = Normalize(auth.ApiKey);
        var apiKeyHeader = Normalize(auth.ApiKeyHeader) ?? "X-API-Key";
        var bearerToken = Normalize(auth.BearerToken);
        var basicUsername = Normalize(auth.BasicAuth?.Username);
        var basicPassword = Normalize(auth.BasicAuth?.Password);

        if (IsTooLong(apiKey, maxTokenLength) ||
            IsTooLong(bearerToken, maxTokenLength) ||
            IsTooLong(basicUsername, maxTokenLength) ||
            IsTooLong(basicPassword, maxTokenLength))
        {
            return null;
        }

        var hasApiKey = !string.IsNullOrEmpty(apiKey);
        if (hasApiKey && !IsValidHttpHeaderName(apiKeyHeader))
        {
            return null;
        }

        var hasBearer = !string.IsNullOrEmpty(bearerToken);
        var hasBasic = auth.BasicAuth != null
                       && !string.IsNullOrEmpty(basicUsername)
                       && !string.IsNullOrEmpty(basicPassword);
        var hasCustomHeaders = auth.CustomHeaders != null && auth.CustomHeaders.Count > 0;

        var mechanismsCount = 0;
        if (hasApiKey) mechanismsCount++;
        if (hasBearer) mechanismsCount++;
        if (hasBasic) mechanismsCount++;
        if (hasCustomHeaders) mechanismsCount++;

        if (mechanismsCount != 1)
        {
            return null;
        }

        var validated = new DataSourceAuthentication();

        if (hasApiKey)
        {
            validated.ApiKey = apiKey;
            validated.ApiKeyHeader = apiKeyHeader;
        }
        else if (hasBearer)
        {
            validated.BearerToken = bearerToken;
        }
        else if (hasBasic)
        {
            validated.BasicAuth = new BasicAuthentication
            {
                Username = basicUsername!,
                Password = basicPassword!
            };
        }
        else if (hasCustomHeaders)
        {
            validated.CustomHeaders = [];
            foreach (var kvp in auth.CustomHeaders!)
            {
                var headerName = Normalize(kvp.Key);
                var headerValue = Normalize(kvp.Value);

                if (string.IsNullOrEmpty(headerName) ||
                    string.IsNullOrEmpty(headerValue) ||
                    !IsValidHttpHeaderName(headerName) ||
                    !IsSafeHeaderValue(headerValue) ||
                    IsTooLong(headerValue, maxTokenLength))
                {
                    return null;
                }

                validated.CustomHeaders[headerName] = headerValue;
            }

            if (validated.CustomHeaders.Count == 0)
            {
                return null;
            }

            if (validated.CustomHeaders.Count > 20)
            {
                return null;
            }
        }

        if ((validated.ApiKey != null && !IsSafeHeaderValue(validated.ApiKey)) ||
            (validated.BearerToken != null && !IsSafeHeaderValue(validated.BearerToken)) ||
            (validated.BasicAuth?.Username != null && !IsSafeHeaderValue(validated.BasicAuth.Username)) ||
            (validated.BasicAuth?.Password != null && !IsSafeHeaderValue(validated.BasicAuth.Password)))
        {
            return null;
        }

        return validated;
    }

    private static bool IsValidHttpHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        const string allowedHeaderTokenSymbols = "!#$%&'*+-.^_`|~";

        foreach (var c in headerName)
        {
            if (char.IsLetterOrDigit(c))
            {
                continue;
            }

            if (allowedHeaderTokenSymbols.Contains(c))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsSafeHeaderValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c == '\r' || c == '\n' || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

}
