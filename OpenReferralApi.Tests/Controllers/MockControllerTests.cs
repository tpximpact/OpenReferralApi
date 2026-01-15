using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OpenReferralApi.Controllers;

namespace OpenReferralApi.Tests.Controllers;

[TestFixture]
public class MockControllerTests
{
    private Mock<ILogger<MockController>> _loggerMock;
    private MockController _controller;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MockController>>();
        _controller = new MockController(_loggerMock.Object);
    }

    [Test]
    public async Task GetServiceMetadata_DefaultRoute_ReturnsOkResult()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetServiceMetadata();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task GetServices_DefaultRoute_ReturnsOkResult()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetServices();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task GetServicesById_WithValidId_ReturnsOkResult()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetServicesById();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task GetTaxonomies_DefaultRoute_ReturnsOkResult()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetTaxonomies();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task GetServiceAtLocations_DefaultRoute_ReturnsOkResult()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetServiceAtLocations();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
