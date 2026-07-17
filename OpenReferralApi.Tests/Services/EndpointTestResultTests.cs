
namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class EndpointTestResultTests
{
    [Test]
    public void ValidationErrors_AggregatesAndDeduplicatesAcrossTestResults()
    {
        var endpoint = new EndpointTestResult
        {
            Path = "/services",
            Method = "GET",
            TestResults =
            [
                new()
                {
                    ValidationResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors =
                        [
                            new() { Path = "data.name", ErrorCode = "E1", Message = "required", Severity = "Error" },
                            new() { Path = "data.postcode", ErrorCode = "W1", Message = "unknown", Severity = "Warning" }
                        ]
                    }
                },
                new()
                {
                    ValidationResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors =
                        [
                            new() { Path = "data.name", ErrorCode = "E1", Message = "required", Severity = "Error" },
                            new() { Path = "data.email", ErrorCode = "E2", Message = "invalid", Severity = "Error" }
                        ]
                    }
                }
            ]
        };

        var flattened = endpoint.ValidationErrors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flattened, Has.Count.EqualTo(3));
            Assert.That(flattened.Any(e => e.Path == "data.name"), Is.True);
            Assert.That(flattened.Any(e => e.Path == "data.postcode"), Is.True);
            Assert.That(flattened.Any(e => e.Path == "data.email"), Is.True);
        }
    }

    [Test]
    public void PrimaryTestResult_PrefersFirstFailingTest_ElseFirstAvailable()
    {
        var passing = new HttpTestResult
        {
            IsSuccessStatusCode = true,
            ValidationResult = new ValidationResult { IsValid = true }
        };

        var failing = new HttpTestResult
        {
            IsSuccessStatusCode = true,
            ValidationResult = new ValidationResult
            {
                IsValid = false,
                Errors = [new() { Path = "data.name", Message = "required", ErrorCode = "E1" }]
            }
        };

        var endpointWithFailure = new EndpointTestResult
        {
            TestResults = [passing, failing]
        };

        var endpointWithoutFailure = new EndpointTestResult
        {
            TestResults = [passing]
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpointWithFailure.PrimaryTestResult, Is.SameAs(failing));
            Assert.That(endpointWithoutFailure.PrimaryTestResult, Is.SameAs(passing));
        }
    }

    [Test]
    public void RefreshFlattenedFields_PreservesFlattenedErrorsBeforeTestResultsClear()
    {
        var endpoint = new EndpointTestResult
        {
            TestResults =
            [
                new()
                {
                    ValidationResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors =
                        [
                            new() { Path = "data.name", ErrorCode = "E1", Message = "required", Severity = "Error" }
                        ]
                    }
                }
            ]
        };

        endpoint.RefreshFlattenedFields();
        endpoint.TestResults.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.ValidationErrors, Has.Count.EqualTo(1));
            Assert.That(endpoint.ValidationErrors[0].Path, Is.EqualTo("data.name"));
        }
    }
}
