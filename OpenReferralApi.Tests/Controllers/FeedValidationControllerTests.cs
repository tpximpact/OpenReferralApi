using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OpenReferralApi.Controllers;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Controllers;

[TestFixture]
public class FeedValidationControllerTests
{
    private Mock<IFeedValidationService> _feedValidationServiceMock;
    private Mock<ILogger<FeedValidationController>> _loggerMock;
    private FeedValidationController _controller;

    [SetUp]
    public void Setup()
    {
        _feedValidationServiceMock = new Mock<IFeedValidationService>();
        _loggerMock = new Mock<ILogger<FeedValidationController>>();

        _controller = new FeedValidationController(
            _feedValidationServiceMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task GetAllFeeds_ReturnsOkWithFeeds()
    {
        // Arrange
        var feeds = new List<ServiceFeed>
        {
            new() { Id = "1", UrlField = "https://example1.com" },
            new() { Id = "2", UrlField = "https://example2.com" }
        };

        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feeds);

        // Act
        var result = await _controller.GetAllFeeds(CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var returnedFeeds = okResult?.Value as List<ServiceFeed>;
        Assert.That(returnedFeeds, Has.Count.EqualTo(2));
    }

    [Test]
    public void GetAllFeeds_WithException_ThrowsException()
    {
        // Arrange
        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert - Exception should propagate to GlobalExceptionHandler
        Assert.ThrowsAsync<Exception>(async () =>
            await _controller.GetAllFeeds(CancellationToken.None));
    }

    [Test]
    public async Task ValidateAllFeeds_WithFeeds_ReturnsValidationSummary()
    {
        // Arrange
        var feeds = new List<ServiceFeed>
        {
            new() { Id = "1", UrlField = "https://example1.com", ActiveField = true },
            new() { Id = "2", UrlField = "https://example2.com", ActiveField = true }
        };

        var validationResults = new List<FeedValidationResult>
        {
            new() { FeedId = "1", IsUp = true, IsValid = true, ResponseTimeMs = 100 },
            new() { FeedId = "2", IsUp = true, IsValid = false, ResponseTimeMs = 150 }
        };

        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feeds);

        _feedValidationServiceMock
            .Setup(x => x.ValidateAndUpdateFeedsAsync(It.IsAny<List<ServiceFeed>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResults);

        // Act
        var result = await _controller.ValidateAllFeeds(CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var summary = okResult?.Value as FeedValidationSummary;
        Assert.That(summary?.TotalFeeds, Is.EqualTo(2));
        Assert.That(summary?.UpFeeds, Is.EqualTo(2));
        Assert.That(summary?.ValidFeeds, Is.EqualTo(1));
        Assert.That(summary?.InvalidFeeds, Is.EqualTo(1));
    }

    [Test]
    public async Task ValidateAllFeeds_WithNoFeeds_ReturnsEmptySummary()
    {
        // Arrange
        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _controller.ValidateAllFeeds(CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var summary = okResult?.Value as FeedValidationSummary;
        Assert.That(summary?.TotalFeeds, Is.EqualTo(0));
        Assert.That(summary?.Message, Does.Contain("No feeds found"));
    }

    [Test]
    public void ValidateAllFeeds_WithException_ThrowsException()
    {
        // Arrange
        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Validation error"));

        // Act & Assert - Exception should propagate to GlobalExceptionHandler
        Assert.ThrowsAsync<Exception>(async () =>
            await _controller.ValidateAllFeeds(CancellationToken.None));
    }

    [Test]
    public async Task ValidateFeed_WithExistingFeed_ReturnsValidationResult()
    {
        // Arrange
        var feedId = "1";
        var feed = new ServiceFeed { Id = feedId, UrlField = "https://example.com" };
        var validationResult = new FeedValidationResult
        {
            FeedId = feedId,
            IsUp = true,
            IsValid = true,
            ResponseTimeMs = 100
        };

        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([feed]);

        _feedValidationServiceMock
            .Setup(x => x.ValidateAndUpdateFeedAsync(feed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateFeed(feedId, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var returnedResult = okResult?.Value as FeedValidationResult;
        Assert.That(returnedResult?.FeedId, Is.EqualTo(feedId));
        Assert.That(returnedResult?.IsValid, Is.True);

        _feedValidationServiceMock.Verify(
            x => x.ValidateAndUpdateFeedAsync(feed, It.IsAny<CancellationToken>()),
            Times.Once);
        _feedValidationServiceMock.Verify(
            x => x.ValidateSingleFeedAsync(It.IsAny<ServiceFeed>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _feedValidationServiceMock.Verify(
            x => x.UpdateFeedStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ValidateFeed_WithNonexistentFeed_ReturnsNotFound()
    {
        // Arrange
        var feedId = "nonexistent";
        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _controller.ValidateFeed(feedId, CancellationToken.None);

        // Assert
        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void ValidateFeed_WithException_ThrowsException()
    {
        // Arrange
        var feedId = "1";
        _feedValidationServiceMock
            .Setup(x => x.GetAllFeedsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Validation error"));

        // Act & Assert - Exception should propagate to GlobalExceptionHandler
        Assert.ThrowsAsync<Exception>(async () =>
            await _controller.ValidateFeed(feedId, CancellationToken.None));
    }
}

