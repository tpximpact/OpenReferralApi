using System.Text.Json.Nodes;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenReferralApi.Models;
using OpenReferralApi.Services;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Tests;

public class PaginationTestingServiceShould
{
    private readonly Mock<IRequestService> _requestServiceMock = new();
    private JsonSerializerSettings serializerSettings;
    private const string Endpoint = "/test-endpoint";
    private JsonNode _pageTestData;
    private JsonNode _pageWithIssuesTestData;
    private JsonNode _firstPageTestData;
    private JsonNode _firstPageWithIssuesTestData;
    private JsonNode _lastPageTestData;
    private JsonNode _lastPageWithIssuesTestData;

    [SetUp]
    public async Task Setup()
    {
        serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        };

        _pageTestData = await ReadTestDataFromFile("pagination.json");
        _pageWithIssuesTestData = await ReadTestDataFromFile("paginationWithIssues.json");
        _firstPageTestData = await ReadTestDataFromFile("paginationFirstPage.json");
        _firstPageWithIssuesTestData = await ReadTestDataFromFile("paginationFirstPageWithIssues.json");
        _lastPageTestData = await ReadTestDataFromFile("paginationLastPage.json");
        _lastPageWithIssuesTestData = await ReadTestDataFromFile("paginationLastPageWithIssues.json");
    }

    [Test]
    public async Task ValidatePagination_Of_Pages_Without_Issues()
    {
        // Arrange
        _requestServiceMock
            .Setup(m => m.GetApiResponse(It.Is<string>(s => s.Contains("page=1"))))
            .ReturnsAsync(_firstPageTestData);
        _requestServiceMock
            .Setup(m => m.GetApiResponse(It.Is<string>(s => !s.Contains("page=1") && !s.Contains("page=4"))))
            .ReturnsAsync(_pageTestData);
        _requestServiceMock
            .Setup(m => m.GetApiResponse(It.Is<string>(s => s.Contains("page=4"))))
            .ReturnsAsync(_lastPageTestData);
        
        var service = new PaginationTestingService(_requestServiceMock.Object);

        // Act
        var result = await service.ValidatePagination("test.com", "/test");


        // Assert
        result.Value.Count.Should().Be(0);
    }

    [Test]
    public async Task ValidatePagination_Of_Pages_With_Issues()
    {
        // Arrange
        _requestServiceMock
            .Setup(m => m.GetApiResponse(It.Is<string>(s => s.Contains("page=1"))))
            .ReturnsAsync(_firstPageWithIssuesTestData);
        _requestServiceMock
            .Setup(m => m.GetApiResponse(It.Is<string>(s => !s.Contains("page=1") && !s.Contains("page=4"))))
            .ReturnsAsync(_pageWithIssuesTestData);
        _requestServiceMock
            .Setup(m => m.GetApiResponse(It.Is<string>(s => s.Contains("page=4"))))
            .ReturnsAsync(_lastPageWithIssuesTestData);
        
        var service = new PaginationTestingService(_requestServiceMock.Object);

        // Act
        var result = await service.ValidatePagination("test.com", "/test");


        // Assert
        result.Value.Count.Should().BeGreaterThan(0);
    }

    [Test]
    [TestCase(true,false,1, 4, 20,  5, false, 5, 0)] // No issues
    [TestCase(false,false,1, 4, 20,  5, false, 5, 1)] // First page flag
    [TestCase(false,false,3, 5, 20,  4, false, 3, 2)] // Items per page (asked for 5), Size != item count
    [TestCase(false,false,1, 4, 20,  6, true, 5, 3)] // First page flag, size, empty flag
    public void ValidatePageDetails(bool firstPage, bool lastPage, int pageNumber, int totalPages, 
        int totalItems, int size, bool empty, int itemCount, int issuesCount)
    {
        // Arrange
        var page = new Page
        {
            FirstPage = firstPage, LastPage = lastPage, PageNumber = pageNumber, TotalPages = totalPages, 
            TotalItems = totalItems, Size = size, Empty = empty,
            Contents = new List<object>()
        };
        for (int i = 0; i < itemCount; i++)
        {
            page.Contents.Add(new object());
        }
        
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);

        // Act
        var result = service.ValidatePageDetails(page, parameters, Endpoint, page.PageNumber, page.TotalPages);

        // Assert
        result.Count().Should().Be(issuesCount);
    }

    [Test]
    [TestCase(false, 2)]
    [TestCase(true, 1)]
    public void Not_Return_Issues_From_IsFirstPageFlagCorrect(bool firstPageFlag, int page)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);

        // Act
        (bool correct, Issue? issue) result = service.IsFirstPageFlagCorrect(firstPageFlag, page, parameters, Endpoint);

        // Assert
        result.correct.Should().BeTrue();
        result.issue.Should().BeNull();
    }

    [Test]
    [TestCase(true, 2)]
    [TestCase(false, 1)]
    public void Return_Issues_From_IsFirstPageFlagCorrect(bool firstPageFlag, int page)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);

        // Act
        (bool correct, Issue? issue) result = service.IsFirstPageFlagCorrect(firstPageFlag, page, parameters, Endpoint);

        // Assert
        result.correct.Should().BeFalse();
        result.issue.Should().NotBeNull();
        result.issue!.Name.Should().Be("First page flag");
        result.issue.Description.Should().Be("Is the 'first_page' flag returned correctly");
        result.issue.Message.Should().Be($"The value of 'first_page' is {firstPageFlag} when the page number is {page}");
        result.issue.Endpoint.Should().Be(Endpoint);
        result.issue.Endpoint.Should().BeEquivalentTo(Endpoint);
        result.issue.Parameters.Should().BeSameAs(parameters);
        result.issue.Parameters.Should().BeEquivalentTo(parameters);
    }
    
    [Test]
    [TestCase(true, 1, 1)]
    [TestCase(true, 10, 10)]
    [TestCase(false, 1, 10)]
    [TestCase(false, 6, 10)]
    public void Not_Return_Issues_From_IstLastPageFlagCorrect(bool lastPageFlag, int page, int totalPages)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IstLastPageFlagCorrect(lastPageFlag, page, totalPages, parameters, Endpoint);
        
        // Assert
        result.correct.Should().BeTrue();
        result.issue.Should().BeNull();
    }
    
    [Test]
    [TestCase(false, 1, 1)]
    [TestCase(false, 10, 10)]
    [TestCase(true, 1, 10)]
    [TestCase(true, 6, 10)]
    public void Return_Issues_From_IstLastPageFlagCorrect(bool lastPageFlag, int page, int totalPages)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IstLastPageFlagCorrect(lastPageFlag, page, totalPages, parameters, Endpoint);
        
        // Assert
        result.correct.Should().BeFalse();
        result.issue.Should().NotBeNull();
        result.issue!.Name.Should().Be("Last page flag");
        result.issue.Description.Should().Be("Is the 'last_page' flag returned correctly");
        result.issue.Message.Should().Be($"The value of 'last_page' is {lastPageFlag} when the page number is {page} of {totalPages}");
        result.issue.Endpoint.Should().Be(Endpoint);
        result.issue.Endpoint.Should().BeEquivalentTo(Endpoint);
        result.issue.Parameters.Should().BeSameAs(parameters);
        result.issue.Parameters.Should().BeEquivalentTo(parameters);
    }

    [Test]
    [TestCase(true, 0)]
    [TestCase(false, 3)]
    public void Not_Return_Issues_From_IsEmptyFlagCorrect(bool emptyFlag, int itemCount)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IsEmptyFlagCorrect(emptyFlag, itemCount, parameters, Endpoint);

        // Assert
        result.correct.Should().BeTrue();
        result.issue.Should().BeNull();
    }

    [Test]
    [TestCase(false, 0)]
    [TestCase(true, 3)]
    public void Return_Issues_From_IsEmptyFlagCorrect(bool emptyFlag, int itemCount)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IsEmptyFlagCorrect(emptyFlag, itemCount, parameters, Endpoint);

        // Assert
        result.correct.Should().BeFalse();
        result.issue.Should().NotBeNull();
        result.issue!.Name.Should().Be("Empty flag");
        result.issue.Description.Should().Be("Is the 'empty' flag returned correctly");
        result.issue.Message.Should().Be($"The value of 'empty' is {emptyFlag} when {itemCount} were returned in the response");
        result.issue.Endpoint.Should().Be(Endpoint);
        result.issue.Endpoint.Should().BeEquivalentTo(Endpoint);
        result.issue.Parameters.Should().BeSameAs(parameters);
        result.issue.Parameters.Should().BeEquivalentTo(parameters);
    }

    [Test]
    [TestCase(5, true, 5)]
    [TestCase(2, true, 5)]
    [TestCase(5, false, 5)]
    public void Not_Return_Issues_From_IsItemsPerPageCorrect(int itemCount, bool lastPageFlag, int perPage)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IsItemsPerPageCorrect(itemCount, lastPageFlag, perPage, parameters, Endpoint);

        // Assert
        result.correct.Should().BeTrue();
        result.issue.Should().BeNull();
    }

    [Test]
    [TestCase(8, true, 5)]
    [TestCase(8, false, 5)]
    [TestCase(2, false, 5)]
    public void Return_Issues_From_IsItemsPerPageCorrect(int itemCount, bool lastPageFlag, int perPage)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IsItemsPerPageCorrect(itemCount, lastPageFlag, perPage, parameters, Endpoint);

        // Assert
        result.correct.Should().BeFalse();
        result.issue.Should().NotBeNull();
        result.issue!.Name.Should().Be("Items per page");
        result.issue.Description.Should().Be("Is the number of items returned per page correct");
        result.issue.Message.Should().Be($"The number of items returned is {itemCount} when {perPage} item(s) were requested in the 'per_page' parameter");
        result.issue.Endpoint.Should().Be(Endpoint);
        result.issue.Endpoint.Should().BeEquivalentTo(Endpoint);
        result.issue.Parameters.Should().BeSameAs(parameters);
        result.issue.Parameters.Should().BeEquivalentTo(parameters);
    }

    [Test]
    [TestCase(5, 5)]
    [TestCase(111, 111)]
    public void Not_Return_Issues_From_IsSizeCorrect(int size, int itemCount)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IsSizeCorrect(size, itemCount, parameters, Endpoint);

        // Assert
        result.correct.Should().BeTrue();
        result.issue.Should().BeNull();
    }

    [Test]
    [TestCase(2, 5)]
    [TestCase(111, 5)]
    public void Return_Issues_From_IsSizeCorrect(int size, int itemCount)
    {
        // Arrange
        var parameters = new Dictionary<string, string>();
        var service = new PaginationTestingService(_requestServiceMock.Object);
        
        // Act
        (bool correct, Issue? issue) result = service.IsSizeCorrect(size, itemCount, parameters, Endpoint);

        // Assert
        result.correct.Should().BeFalse();
        result.issue.Should().NotBeNull();
        result.issue!.Name.Should().Be("Item count");
        result.issue.Description.Should().Be("Does the number of items returned match the 'size' value in the response");
        result.issue.Message.Should().Be($"The value of 'size' is {size} when {itemCount} item(s) were returned in the response content");
        result.issue.Endpoint.Should().Be(Endpoint);
        result.issue.Endpoint.Should().BeEquivalentTo(Endpoint);
        result.issue.Parameters.Should().BeSameAs(parameters);
        result.issue.Parameters.Should().BeEquivalentTo(parameters);
    }

    private async Task<JsonNode> ReadTestDataFromFile(string fileName)
    {
        try
        {
            var filePath = @$"../../../TestData/{fileName}";
            // Open the text file using a stream reader.
            using StreamReader reader = new(filePath);

            // Read the stream as a string.
            var fileData = await reader.ReadToEndAsync();

            var testData = JsonNode.Parse(fileData);

            return testData!;
        }
        catch (IOException e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
            throw;
        }
    }
    
}