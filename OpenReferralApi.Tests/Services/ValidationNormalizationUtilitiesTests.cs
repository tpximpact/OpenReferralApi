using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class ValidationPathNormalizerTests
{
    [Test]
    public void NormalizeArrayIndexes_WithNullOrEmpty_ReturnsEmptyString()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValidationPathNormalizer.NormalizeArrayIndexes(null), Is.EqualTo(string.Empty));
            Assert.That(ValidationPathNormalizer.NormalizeArrayIndexes(string.Empty), Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public void NormalizeArrayIndexes_WithoutBracketSegments_ReturnsInputUnchanged()
    {
        const string input = "content.id";

        var normalized = ValidationPathNormalizer.NormalizeArrayIndexes(input);

        Assert.That(normalized, Is.EqualTo(input));
    }

    [Test]
    public void NormalizeArrayIndexes_WithNumericIndexes_ReplacesWithEmptyArrayMarkers()
    {
        const string input = "contents[3].pc_targetAudience[12].name";

        var normalized = ValidationPathNormalizer.NormalizeArrayIndexes(input);

        Assert.That(normalized, Is.EqualTo("contents[].pc_targetAudience[].name"));
    }

    [Test]
    public void NormalizeArrayIndexes_WithNonNumericBracketSegments_DoesNotReplaceThem()
    {
        const string input = "paths[/services].get.responses[default].content";

        var normalized = ValidationPathNormalizer.NormalizeArrayIndexes(input);

        Assert.That(normalized, Is.EqualTo(input));
    }
}

[TestFixture]
public class ValidationErrorNormalizerTests
{
    private static readonly string[] ExpectedPaths = ["data[].name", "data[].url"];
    private static readonly string[] ExpectedErrorCodes = ["A", "B"];
    [Test]
    public void NormalizeAndDeduplicateByPath_KeepsFirstErrorPerNormalizedPath()
    {
        var errors = new List<ValidationError>
        {
            new()
            {
                Path = "data[0].name",
                Message = "data[0].name is required",
                ErrorCode = "VALIDATION_ERROR",
                Severity = "Error",
                LineNumber = 10,
                ColumnNumber = 20
            },
            new()
            {
                Path = "data[1].name",
                Message = "data[1].name is required",
                ErrorCode = "VALIDATION_ERROR",
                Severity = "Error",
                LineNumber = 11,
                ColumnNumber = 21
            }
        };

        var normalized = ValidationErrorNormalizer.NormalizeAndDeduplicateByPath(errors);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(normalized, Has.Count.EqualTo(1));
            Assert.That(normalized[0].Path, Is.EqualTo("data[].name"));
            Assert.That(normalized[0].Message, Is.EqualTo("data[].name is required"));
            Assert.That(normalized[0].LineNumber, Is.EqualTo(10));
            Assert.That(normalized[0].ColumnNumber, Is.EqualTo(20));
        }
    }

    [Test]
    public void NormalizeAndDeduplicateByPath_PreservesDistinctNormalizedPaths()
    {
        var errors = new List<ValidationError>
        {
            new() { Path = "data[0].name", Message = "m1", ErrorCode = "A", Severity = "Error" },
            new() { Path = "data[0].url", Message = "m2", ErrorCode = "B", Severity = "Warning" }
        };

        var normalized = ValidationErrorNormalizer.NormalizeAndDeduplicateByPath(errors);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(normalized.Select(e => e.Path), Is.EquivalentTo(ExpectedPaths));
            Assert.That(normalized.Select(e => e.ErrorCode), Is.EquivalentTo(ExpectedErrorCodes));
        }
    }

    [Test]
    public void NormalizeAndDeduplicateByPath_HandlesNonNumericBracketSegmentsWithoutMutatingThem()
    {
        var errors = new List<ValidationError>
        {
            new()
            {
                Path = "paths[/services].get.responses[0].content",
                Message = "paths[/services].get.responses[0].content invalid",
                ErrorCode = "V",
                Severity = "Error"
            }
        };

        var normalized = ValidationErrorNormalizer.NormalizeAndDeduplicateByPath(errors);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(normalized, Has.Count.EqualTo(1));
            Assert.That(normalized[0].Path, Is.EqualTo("paths[/services].get.responses[].content"));
            Assert.That(normalized[0].Message, Is.EqualTo("paths[/services].get.responses[].content invalid"));
        }
    }

    [Test]
    public void NormalizeAndDeduplicateByPath_PreservesSourceIdentifier()
    {
        var errors = new[]
        {
                new ValidationError
                {
                    Path = "$.foo[0]",
                    Message = "Bad path $.foo[0]",
                    ErrorCode = "JSON_STRUCTURE_VIOLATION",
                    Severity = "Error",
                    SourceIdentifier = "request.jsonData (object)"
                }
            };

        var result = ValidationErrorNormalizer.NormalizeAndDeduplicateByPath(errors);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].SourceIdentifier, Is.EqualTo("request.jsonData (object)"));
        }
    }
}
