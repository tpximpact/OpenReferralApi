using OpenReferralApi.Core.Helpers;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class TextSanitizerTests
{
    [Test]
    public void SanitizeExceptionMessage_WithNullOrEmpty_ReturnsEmptyString()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TextSanitizer.SanitizeExceptionMessage(null), Is.EqualTo(string.Empty));
            Assert.That(TextSanitizer.SanitizeExceptionMessage(string.Empty), Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public void SanitizeExceptionMessage_RemovesControlCharacters()
    {
        var input = "bad\r\nmessage\twith\u0001controls";

        var sanitized = TextSanitizer.SanitizeExceptionMessage(input);

        Assert.That(sanitized, Is.EqualTo("badmessagewithcontrols"));
    }

    [Test]
    public void SanitizeExceptionMessage_TruncatesLongInput()
    {
        var input = new string('a', 510);

        var sanitized = TextSanitizer.SanitizeExceptionMessage(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sanitized, Has.Length.EqualTo(514));
            Assert.That(sanitized.EndsWith("...(truncated)", StringComparison.Ordinal), Is.True);
        }
    }

    [Test]
    public void SanitizeForLogging_WithNullOrEmpty_ReturnsEmptyString()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TextSanitizer.SanitizeForLogging(null), Is.EqualTo(string.Empty));
            Assert.That(TextSanitizer.SanitizeForLogging(string.Empty), Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public void SanitizeForLogging_RemovesCrLfOnly()
    {
        const string input = "line1\r\nline2\nline3\rline4\tkeep-tab";

        var sanitized = TextSanitizer.SanitizeForLogging(input);

        Assert.That(sanitized, Is.EqualTo("line1line2line3line4\tkeep-tab"));
    }
}
