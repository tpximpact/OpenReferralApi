using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class AuthenticationValidationLog
    {
        [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Authentication failed: {Reason}")]
        public static partial void AuthenticationFailed(this ILogger logger, string reason);

        [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Missing authentication header: {HeaderName}")]
        public static partial void MissingAuthenticationHeader(this ILogger logger, string headerName);
    }
}
