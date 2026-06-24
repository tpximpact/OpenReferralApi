namespace OpenReferralApi.Logging
{
    internal static partial class StartupLogger
    {
        // The Source Generator sees this attribute and writes the 
        // high-performance implementation for you at compile-time.
        [LoggerMessage(
            Level = LogLevel.Information, 
            Message = "OpenApiValidation settings at startup: {Settings}")]
        public static partial void LogSettings(ILogger logger, object settings);
    }
}
