using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class OpenApiBootstrapLog
    {
        [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Bootstrap discovery resolved schema URL {SchemaUrl} with profile context {ProfileReason}")]
        public static partial void BootstrapDiscoveryResolved(this ILogger logger, string schemaUrl, string profileReason);
    }
}
