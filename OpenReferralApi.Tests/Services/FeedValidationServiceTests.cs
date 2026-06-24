using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class FeedValidationServiceTests
{
    private Mock<IMongoClient> _mongoClientMock = null!;
    private Mock<IMongoDatabase> _databaseMock = null!;
    private Mock<IMongoCollection<ServiceFeed>> _collectionMock = null!;
    private Mock<IOpenApiValidationService> _validationServiceMock = null!;
    private Mock<ILogger<FeedValidationService>> _loggerMock = null!;
    private Mock<IAsyncCursor<ServiceFeed>> _cursorMock = null!;
    private FeedValidationService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mongoClientMock = new Mock<IMongoClient>();
        _databaseMock = new Mock<IMongoDatabase>();
        _collectionMock = new Mock<IMongoCollection<ServiceFeed>>();
        _validationServiceMock = new Mock<IOpenApiValidationService>();
        _loggerMock = new Mock<ILogger<FeedValidationService>>();
        _cursorMock = new Mock<IAsyncCursor<ServiceFeed>>();

        var dbOptionsMock = new Mock<IOptions<DatabaseOptions>>();
        dbOptionsMock.Setup(o => o.Value).Returns(new DatabaseOptions
        {
            DatabaseName = "TestDb",
            ServicesCollection = "TestCollection"
        });

        _mongoClientMock
            .Setup(c => c.GetDatabase(It.IsAny<string>(), null))
            .Returns(_databaseMock.Object);
            
        _databaseMock
            .Setup(d => d.GetCollection<ServiceFeed>(It.IsAny<string>(), null))
            .Returns(_collectionMock.Object);

        // Mock MongoDB FindAsync to return an existing feed, allowing the service to proceed to the status update phase
        _cursorMock.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cursorMock.SetupGet(c => c.Current).Returns([new ServiceFeed { Id = "test-id" }]);
        
        _collectionMock
            .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<ServiceFeed>>(), It.IsAny<FindOptions<ServiceFeed, ServiceFeed>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_cursorMock.Object);

        // Mock MongoDB UpdateOneAsync to simulate successful database status writes
        _collectionMock
            .Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<ServiceFeed>>(), It.IsAny<UpdateDefinition<ServiceFeed>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        // Mock OpenAPI Validation to simulate successful API evaluation
        _validationServiceMock
            .Setup(v => v.ValidateOpenApiSpecificationAsync(It.IsAny<OpenApiValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenApiValidationResult { IsValid = true, EndpointTests = [] });

        _service = new FeedValidationService(
            _mongoClientMock.Object,
            dbOptionsMock.Object,
            _validationServiceMock.Object,
            _loggerMock.Object);
    }
}