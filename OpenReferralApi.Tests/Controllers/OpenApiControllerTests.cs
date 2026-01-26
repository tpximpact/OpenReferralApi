using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OpenReferralApi.Controllers;
using OpenReferralApi.Core.Models;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Controllers;

[TestFixture]
public class OpenApiControllerTests
{
    private Mock<IOpenApiValidationService> _validationServiceMock;
    private Mock<ILogger<OpenApiController>> _loggerMock;
    private Mock<IOpenApiDiscoveryService> _discoveryServiceMock;
    private Mock<IOpenApiToValidationResponseMapper> _mapperMock;
    private OpenApiController _controller;

    [SetUp]
    public void Setup()
    {
        _validationServiceMock = new Mock<IOpenApiValidationService>();
        _loggerMock = new Mock<ILogger<OpenApiController>>();
        _discoveryServiceMock = new Mock<IOpenApiDiscoveryService>();
        _mapperMock = new Mock<IOpenApiToValidationResponseMapper>();

        _controller = new OpenApiController(
            _validationServiceMock.Object,
            _loggerMock.Object,
            _discoveryServiceMock.Object,
            _mapperMock.Object);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://api.example.com",
            OpenApiSchemaUrl = "https://api.example.com/openapi.json"
        };

        var validationResult = new OpenApiValidationResult
        {
            IsValid = true
        };
        
        _validationServiceMock
            .Setup(x => x.ValidateOpenApiSpecificationAsync(It.IsAny<OpenApiValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        _mapperMock
            .Setup(x => x.MapToValidationResponse(It.IsAny<OpenApiValidationResult>()))
            .Returns(new object());

        // Act
        var result = await _controller.ValidateOpenApiSpecificationAsync(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _validationServiceMock.Verify(x => x.ValidateOpenApiSpecificationAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithMissingBaseUrl_ReturnsBadRequest()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            BaseUrl = null,
            OpenApiSchemaUrl = null
        };

        _discoveryServiceMock
            .Setup(x => x.DiscoverOpenApiUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, null));

        // Act
        var result = await _controller.ValidateOpenApiSpecificationAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task ValidateOpenApiSpecificationAsync_WithDiscovery_DiscoversOpenApiUrl()
    {
        // Arrange
        var request = new OpenApiValidationRequest
        {
            BaseUrl = "https://api.example.com",
            OpenApiSchemaUrl = null
        };

        var discoveredUrl = "https://api.example.com/openapi.json";
        _discoveryServiceMock
            .Setup(x => x.DiscoverOpenApiUrlAsync(request.BaseUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((discoveredUrl, "auto-discovered"));

        var validationResult = new OpenApiValidationResult
        {
            IsValid = true
        };
        
        _validationServiceMock
            .Setup(x => x.ValidateOpenApiSpecificationAsync(It.IsAny<OpenApiValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        _mapperMock
            .Setup(x => x.MapToValidationResponse(It.IsAny<OpenApiValidationResult>()))
            .Returns(new object());

        // Act
        var result = await _controller.ValidateOpenApiSpecificationAsync(request);

        // Assert
        request.OpenApiSchemaUrl.Should().Be(discoveredUrl);
        result.Result.Should().BeOfType<OkObjectResult>();
        _discoveryServiceMock.Verify(x => x.DiscoverOpenApiUrlAsync(request.BaseUrl, It.IsAny<CancellationToken>()), Times.Once);
    }
}
