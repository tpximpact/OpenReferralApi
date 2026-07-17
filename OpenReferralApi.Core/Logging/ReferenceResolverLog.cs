using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class ReferenceResolverLog
    {
        [LoggerMessage(EventId = 13000, Level = LogLevel.Debug, Message = "Could not resolve internal reference {Ref}, keeping as-is")]
        public static partial void CouldNotResolveInternalReference(this ILogger logger, string @ref);

        [LoggerMessage(EventId = 13001, Level = LogLevel.Warning, Message = "Cannot resolve internal reference without root document: {Ref}")]
        public static partial void CannotResolveWithoutRootDocument(this ILogger logger, string @ref);

        [LoggerMessage(EventId = 13002, Level = LogLevel.Debug, Message = "Circular reference detected: {Ref}")]
        public static partial void CircularReferenceDetected(this ILogger logger, string @ref);

        [LoggerMessage(EventId = 13003, Level = LogLevel.Warning, Message = "Failed to resolve internal reference path: {Ref} at part: {Part}")]
        public static partial void FailedToResolveInternalReferencePath(this ILogger logger, string @ref, string part);

        [LoggerMessage(EventId = 13004, Level = LogLevel.Warning, Message = "Invalid array index in reference: {Ref} at part: {Part}")]
        public static partial void InvalidArrayIndexInReference(this ILogger logger, string @ref, string part);

        [LoggerMessage(EventId = 13005, Level = LogLevel.Warning, Message = "Cannot navigate through non-object/non-array in reference: {Ref}")]
        public static partial void CannotNavigateThroughNonObject(this ILogger logger, string @ref);

        [LoggerMessage(EventId = 13006, Level = LogLevel.Warning, Message = "Failed to resolve internal anchor reference: {Ref}")]
        public static partial void FailedToResolveAnchorReference(this ILogger logger, string @ref);

        [LoggerMessage(EventId = 13007, Level = LogLevel.Debug, Message = "Circular reference detected: {Ref}")]
        public static partial void CircularExternalReferenceDetected(this ILogger logger, string @ref);

        [LoggerMessage(EventId = 13008, Level = LogLevel.Warning, Message = "Failed to load schema: {Location}")]
        public static partial void FailedToLoadSchema(this ILogger logger, string location);

        [LoggerMessage(EventId = 13009, Level = LogLevel.Warning, Message = "Schema file not found: {Path}")]
        public static partial void SchemaFileNotFound(this ILogger logger, string path);

        [LoggerMessage(EventId = 13010, Level = LogLevel.Error, Message = "Failed to load local schema file: {Path}")]
        public static partial void FailedToLoadLocalSchemaFile(this ILogger logger, Exception exception, string path);

        [LoggerMessage(EventId = 13011, Level = LogLevel.Error, Message = "Circular schema reference detected: {Ref}. Resolution path: {Path}")]
        public static partial void CircularReferenceDetectedWithPath(this ILogger logger, string @ref, string path);

        [LoggerMessage(EventId = 13012, Level = LogLevel.Error, Message = "Circular external schema reference detected: {Ref}. Resolution path: {Path}")]
        public static partial void CircularExternalReferenceDetectedWithPath(this ILogger logger, string @ref, string path);

        [LoggerMessage(EventId = 13013, Level = LogLevel.Warning, Message = "Cyclic reference detected during on-the-fly lookup: {Ref}")]
        public static partial void CyclicReferenceDetectedDuringLookup(this ILogger logger, string @ref);
    }
}
