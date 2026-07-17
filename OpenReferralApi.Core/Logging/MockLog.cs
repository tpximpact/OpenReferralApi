using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class MockLog
    {
        [LoggerMessage(EventId = 4000, Level = LogLevel.Debug, Message = "Reading mock JSON file: {FilePath}")]
        public static partial void ReadingMockJsonFile(this ILogger logger, string filePath);

        [LoggerMessage(EventId = 4001, Level = LogLevel.Error, Message = "Mock file not found: {FilePath}")]
        public static partial void MockFileNotFound(this ILogger logger, Exception exception, string filePath);

        [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Error reading mock file: {FilePath}")]
        public static partial void ErrorReadingMockFile(this ILogger logger, Exception exception, string filePath);

        [LoggerMessage(EventId = 4003, Level = LogLevel.Error, Message = "Unexpected error reading mock file: {FilePath}")]
        public static partial void UnexpectedErrorReadingMockFile(this ILogger logger, Exception exception, string filePath);
    }
}
