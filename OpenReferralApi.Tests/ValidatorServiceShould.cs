using FluentAssertions;
using Moq;
using OpenReferralApi.Services;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Tests;

public class ValidatorServiceShould
{
    private readonly IRequestService _requestServiceMock = new RequestServiceMock();

    [SetUp]
    public void Setup()
    {
        
    }

    [Test]
    public async Task Pass_valid_services()
    {
        // Arrange
        var url = "https://pass.org";
        var validatorService = new ValidatorService(_requestServiceMock);
        
        // Act
        var result = await validatorService.ValidateService(url);

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
        var validatorService = new ValidatorService(_requestServiceMock);
        
        // Act
        var result = await validatorService.ValidateService(url);

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
        var validatorService = new ValidatorService(_requestServiceMock);
        
        // Act
        var result = await validatorService.ValidateService(url);

        // Assert
        result.Value.Service.Url.Should().Be(url);
        result.Value.Service.IsValid.Should().BeTrue();
        result.Value.TestSuites.Count.Should().Be(2);
        result.Value.TestSuites[0].Success.Should().BeTrue();
        result.Value.TestSuites[1].Success.Should().BeFalse();
    }
}