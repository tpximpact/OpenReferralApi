using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Core.Services;

internal static class ValidationErrorNormalizer
{
    internal static List<ValidationError> NormalizeAndDeduplicateByPath(IEnumerable<ValidationError> errors)
    {
        var capacity = errors is ICollection<ValidationError> collection ? collection.Count : 0;
        var seenPaths = capacity > 0
            ? new HashSet<string>(capacity, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var deduplicatedErrors = capacity > 0
            ? new List<ValidationError>(capacity)
            : [];

        foreach (var error in errors)
        {
            var normalizedPath = ValidationPathNormalizer.NormalizeArrayIndexes(error.Path);

            // Keep the first validation error encountered for each normalized path.
            if (!seenPaths.Add(normalizedPath))
            {
                continue;
            }

            // Normalize message only for kept entries to avoid work for discarded duplicates.
            deduplicatedErrors.Add(new ValidationError
            {
                Path = normalizedPath,
                Message = ValidationPathNormalizer.NormalizeArrayIndexes(error.Message),
                ErrorCode = error.ErrorCode,
                Severity = error.Severity,
                LineNumber = error.LineNumber,
                ColumnNumber = error.ColumnNumber,
                SourceIdentifier = error.SourceIdentifier
            });
        }

        return deduplicatedErrors;
    }
}
