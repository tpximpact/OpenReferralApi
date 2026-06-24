using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class AuthenticationValidationServiceTests
{
    private Mock<ILogger<AuthenticationValidationService>> _loggerMock = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<AuthenticationValidationService>>();
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WhenFeatureDisabled_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: false);

        var auth = new DataSourceAuthentication
        {
            ApiKey = "token"
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WhenAuthIsNull_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var result = service.TryGetValidatedRequestAuthentication("schema", null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithValidApiKey_ReturnsSanitizedCopy()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            ApiKey = "  token  ",
            ApiKeyHeader = "  X-API-Key  "
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.SameAs(auth));
            Assert.That(result?.ApiKey, Is.EqualTo("token"));
            Assert.That(result?.ApiKeyHeader, Is.EqualTo("X-API-Key"));
            Assert.That(result?.BearerToken, Is.Null);
        }
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithApiKeyAndDefaultHeader_UsesDefault()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            ApiKey = "token"
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.ApiKeyHeader, Is.EqualTo("X-API-Key"));
        }
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithInvalidHeaderName_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            ApiKey = "token",
            ApiKeyHeader = "Bad Header"
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithMultipleMechanisms_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            ApiKey = "token",
            BearerToken = "bearer"
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithOversizedToken_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            BearerToken = new string('a', 4097)
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithUnsafeHeaderValue_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            ApiKey = "tok\r\nen"
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithValidBasicAuth_ReturnsSanitizedCopy()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            BasicAuth = new BasicAuthentication
            {
                Username = "  user  ",
                Password = "  pass  "
            }
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.BasicAuth, Is.Not.Null);
            Assert.That(result?.BasicAuth?.Username, Is.EqualTo("user"));
            Assert.That(result?.BasicAuth?.Password, Is.EqualTo("pass"));
        }
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithInvalidCustomHeaderValue_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            CustomHeaders = new Dictionary<string, string>
            {
                ["X-Test"] = "bad\nvalue"
            }
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithTooManyCustomHeaders_ReturnsNull()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var headers = Enumerable.Range(0, 21)
            .ToDictionary(i => $"X-H-{i}", i => "ok");

        var auth = new DataSourceAuthentication
        {
            CustomHeaders = headers
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetValidatedRequestAuthentication_WithValidCustomHeaders_ReturnsCopy()
    {
        var service = CreateService(allowUserSuppliedAuth: true);

        var auth = new DataSourceAuthentication
        {
            CustomHeaders = new Dictionary<string, string>
            {
                [" X-Token "] = " abc "
            }
        };

        var result = service.TryGetValidatedRequestAuthentication("schema", auth);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.CustomHeaders, Is.Not.Null);
            Assert.That(result?.CustomHeaders, Has.Count.EqualTo(1));
            Assert.That(result?.CustomHeaders?.ContainsKey("X-Token"), Is.True);
            Assert.That(result?.CustomHeaders?["X-Token"], Is.EqualTo("abc"));
        }
    }

    private AuthenticationValidationService CreateService(bool allowUserSuppliedAuth)
    {
        var options = Options.Create(new OpenApiValidationServerOptions
        {
            AllowUserSuppliedAuth = allowUserSuppliedAuth
        });

        return new AuthenticationValidationService(_loggerMock.Object, options);
    }
}
