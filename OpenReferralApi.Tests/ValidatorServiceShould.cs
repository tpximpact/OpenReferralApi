using System.Text.Json.Nodes;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenReferralApi.Models;
using OpenReferralApi.Services;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Tests;

public class ValidatorServiceShould
{
    private readonly ILogger<ValidatorService> _logger = new Logger<ValidatorService>(new NullLoggerFactory());
    private IRequestService _requestServiceMock = new RequestServiceMock();
    private Mock<ITestProfileService> _testProfileServiceMock = new Mock<ITestProfileService>();
    private Mock<IPaginationTestingService> _paginationTestinServiceMock = new Mock<IPaginationTestingService>();
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
        _testProfileServiceMock
            .Setup(m => m.SelectTestSchema(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("V3.0-UK-Test", "reason output"));

        var testProfile = new TestProfile
        {
            Profile = "V3.0-UK-Test",
            TestGroups = new List<TestCaseGroup>
            {
                new TestCaseGroup
                {
                    Name = "TestTestGroup",
                    Description = "A description of a test test group",
                    MessageLevel = "Error",
                    Required = true,
                    Tests = new List<TestCase>
                    {
                        new TestCase
                        {
                            Name = "TestTest",
                            Description = "A description of a test tes case",
                            Endpoint = "/",
                            Pagination = false,
                            Schema = "V3.0-UK/api_details.json"
                        }
                    }
                }
            }
        };

        _testProfileServiceMock
            .Setup(m => m.ReadTestProfileFromFile("V3.0-UK-Test"))
            .ReturnsAsync(testProfile);
        
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileServiceMock.Object, 
            _paginationTestinServiceMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(inputUrl, "HSDS-UK-3.0-Test");

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
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileServiceMock.Object, 
            _paginationTestinServiceMock.Object);
        
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
        _testProfileServiceMock
            .Setup(m => m.SelectTestSchema(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("V3.0-UK-Test", "reason output"));

        var testProfile = new TestProfile
        {
            Profile = "V3.0-UK-Test",
            TestGroups = new List<TestCaseGroup>
            {
                new TestCaseGroup
                {
                    Name = "TestTestGroup",
                    Description = "A description of a test test group",
                    MessageLevel = "Error",
                    Required = true,
                    Tests = new List<TestCase>
                    {
                        new TestCase
                        {
                            Name = "TestTest",
                            Description = "A description of a test tes case",
                            Endpoint = "/",
                            Pagination = false,
                            Schema = "V3.0-UK/api_details.json"
                        }
                    }
                }
            }
        };

        _testProfileServiceMock
            .Setup(m => m.ReadTestProfileFromFile("V3.0-UK-Test"))
            .ReturnsAsync(testProfile);
        
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileServiceMock.Object, 
            _paginationTestinServiceMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(url, "V3-UK");

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeTrue();
        result.Value.TestSuites.Count.Should().Be(1);
        result.Value.TestSuites[0].Success.Should().BeTrue();
    }

    [Test]
    public async Task Fail_services_with_Level1_issues()
    {
        // Arrange
        var url = "https://fail.org";
        
        _testProfileServiceMock
            .Setup(m => m.SelectTestSchema(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("V3.0-UK-Test", "reason output"));

        var testProfile = new TestProfile
        {
            Profile = "V3.0-UK-Test",
            TestGroups = new List<TestCaseGroup>
            {
                new TestCaseGroup
                {
                    Name = "TestTestGroup",
                    Description = "A description of a test test group",
                    MessageLevel = "Error",
                    Required = true,
                    Tests = new List<TestCase>
                    {
                        new TestCase
                        {
                            Name = "TestTest",
                            Description = "A description of a test tes case",
                            Endpoint = "/",
                            Pagination = false,
                            Schema = "V3.0-UK/api_details.json"
                        }
                    }
                }
            }
        };

        _testProfileServiceMock
            .Setup(m => m.ReadTestProfileFromFile("V3.0-UK-Test"))
            .ReturnsAsync(testProfile);
        
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileServiceMock.Object, 
            _paginationTestinServiceMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(url, "V3-UK");

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeFalse();
        result.Value.TestSuites.Count.Should().Be(1);
        result.Value.TestSuites[0].Success.Should().BeFalse();
    }

    [Test]
    public async Task Warn_services_with_level2_issues()
    {
        // Arrange
        var url = "https://fail.org";
        
        _testProfileServiceMock
            .Setup(m => m.SelectTestSchema(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("V3.0-UK-Test", "reason output"));

        var testProfile = new TestProfile
        {
            Profile = "V3.0-UK-Test",
            TestGroups = new List<TestCaseGroup>
            {
                new TestCaseGroup
                {
                    Name = "TestTestGroup",
                    Description = "A description of a test test group",
                    MessageLevel = "Warning",
                    Required = false,
                    Tests = new List<TestCase>
                    {
                        new TestCase
                        {
                            Name = "TestTest",
                            Description = "A description of a test tes case",
                            Endpoint = "/",
                            Pagination = false,
                            Schema = "V3.0-UK/api_details.json"
                        }
                    }
                }
            }
        };

        _testProfileServiceMock
            .Setup(m => m.ReadTestProfileFromFile("V3.0-UK-Test"))
            .ReturnsAsync(testProfile);
        var validatorService = new ValidatorService(_logger, _requestServiceMock, _testProfileServiceMock.Object, 
            _paginationTestinServiceMock.Object);
        
        // Act
        var result = await validatorService.ValidateService(url, "V3-UK");

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeTrue();
        result.Value.TestSuites.Count.Should().Be(1);
        result.Value.TestSuites[0].Success.Should().BeFalse();
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