using System.Text.Json.Nodes;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenReferralApi.Services;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Tests;

public class ValidatorServiceShould
{
    private readonly ILogger<ValidatorService> _logger = new Logger<ValidatorService>(new NullLoggerFactory());
    private IRequestService _requestServiceMock = new RequestServiceMock();
    private Mock<ITestProfileService> _testProfileMock = new Mock<ITestProfileService>();
    private const string MockPath = "Mocks/V3.0-UK-";

    [SetUp]
    public void Setup() { }

    [Test]
    [TestCase("https://pass.org", "https://pass.org")]
    [TestCase("https://pass.org/", "https://pass.org")]
    [TestCase("https://pass.org///", "https://pass.org")]
    [TestCase("http://pass.org/", "http://pass.org")]
    public async Task Remove_trailing_slashes_from_urls(string inputUrl, string expectedUrl)
    {
        // Arrange
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(inputUrl, "V3-UK");

        // Assert
        result.Value.Service.IsValid.Should().BeTrue();
        result.Value.Service.Url.Should().Be(expectedUrl);
    }

    [Test]
    [TestCase("htt/pass.org")]
    [TestCase("pass-org//")]
    [TestCase(@"C:\Users\User1\Documents\")]
    public async Task Reject_invalid_urls(string inputUrl)
    {
        // Arrange
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(inputUrl, "V3-UK");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Message.Should().Be("Invalid URL provided");
    }

    [Test]
    public async Task Pass_valid_services()
    {
        // Arrange
        var url = "https://pass.org";
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(url, "V3-UK");

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeTrue();
        result.Value.TestSuites.Count.Should().Be(2);
        result.Value.TestSuites[0].Success.Should().BeTrue();
        result.Value.TestSuites[1].Success.Should().BeTrue();
    }

    [Test]
    public async Task Fail_services_with_Level1_issues()
    {
        // Arrange
        var url = "https://fail.org";
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(url, "V3-UK");

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeFalse();
        result.Value.TestSuites.Count.Should().Be(2);
        result.Value.TestSuites[0].Success.Should().BeFalse();
        result.Value.TestSuites[1].Success.Should().BeFalse();
    }

    [Test]
    public async Task Warn_services_with_level2_issues()
    {
        // Arrange
        var url = "https://warn.org";
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(url, "V3-UK");

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeTrue();
        result.Value.TestSuites.Count.Should().Be(2);
        result.Value.TestSuites[0].Success.Should().BeTrue();
        result.Value.TestSuites[1].Success.Should().BeFalse();
    }
    
    private async Task<Result<JsonNode>> ReadJsonFile(string filePath)
    {
        try
        {
            // Open the text file using a stream reader.
            using StreamReader reader = new(filePath);

            // Read the stream as a string.
            var mock = await reader.ReadToEndAsync();

            var mockResponse = JsonNode.Parse(mock);

            return mockResponse;
        }
        catch (IOException e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
        }

        return Result.Fail("Sorry, something went wrong when trying to return the mock");
    } 
}