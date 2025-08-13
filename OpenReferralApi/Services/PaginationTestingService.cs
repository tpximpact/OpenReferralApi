using FluentResults;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenReferralApi.Constants;
using OpenReferralApi.Models;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class PaginationTestingService : IPaginationTestingService
{
    private readonly IRequestService _requestService;
    private readonly JsonSerializerSettings _serializerSettings;
    private const int PageLoopLimit = 3; 
    private const int PerPage = 5;

    public PaginationTestingService(IRequestService requestService)
    {
        _requestService = requestService;
        _serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        };
    }

    public async Task<Result<List<Issue>>> ValidatePagination(string baseUrl, string endpoint, string schemaVersion)
                // Validate the pagination for the given endpoint
    {
        var issues = new List<Issue>();
        // var url = baseUrl + endpoint;
        var pageNumber = 1;
        var random = new Random();
        var parameters = new Dictionary<string, string>
        {
            { "per_page", PerPage.ToString() }, 
            { "page", pageNumber.ToString() }
        };
            
        var url = QueryHelpers.AddQueryString(baseUrl + endpoint, parameters!);
        
        // Request first page
        var response = await _requestService.GetApiResponse(url);
        if (response.IsFailed)
        {
            issues.Add(new Issue()
            {
                Name = "API response",
                Description = $"An error occurred when making a request to the `{endpoint}` endpoint",
                Message = response.Errors.First().Message,
                Parameters = parameters,
                Endpoint = url
            });
            return issues;
        }

        IPage? firstPage = schemaVersion == HSDSUKVersions.V3
            ? JsonConvert.DeserializeObject<PageV3>(response.ValueOrDefault.ToJsonString(), _serializerSettings)
            : JsonConvert.DeserializeObject<PageV1>(response.ValueOrDefault.ToJsonString());

        var lastPageNumber = firstPage!.TotalPages;
        issues.AddRange(ValidatePageDetails(firstPage, parameters, endpoint, pageNumber, lastPageNumber));

        // If there is only one page no need to continue testing
        if (firstPage.TotalPages == 1)
            return issues;
        
        // Request several pages and check the pagination meta data 
        for (var pageCount = 1; pageCount <= PageLoopLimit; pageCount++)
        {
            // If there are only two pages skip to testing the last page
            if (firstPage.TotalPages == 2)
                break;
            
            pageNumber = random.Next(2, firstPage.TotalPages - 1);
            parameters["page"] = pageNumber.ToString();
            url = QueryHelpers.AddQueryString(baseUrl + endpoint, parameters!);
            
            response = await _requestService.GetApiResponse(url);
            if (response.IsFailed)
            {
                issues.Add(new Issue()
                {
                    Name = "API response",
                    Description = $"An error occurred when making a request to the `{endpoint}` endpoint",
                    Message = response.Errors.First().Message,
                    Parameters = parameters,
                    Endpoint = url
                });
                return issues;
            }

            IPage? page = schemaVersion == HSDSUKVersions.V3
                ? JsonConvert.DeserializeObject<PageV3>(response.Value.ToJsonString(), _serializerSettings)
                : JsonConvert.DeserializeObject<PageV1>(response.Value.ToJsonString());

            if (page != null)
            {
                issues.AddRange(ValidatePageDetails(page, parameters, endpoint, pageNumber, lastPageNumber));
            }
        }
        
        // Request last page
        parameters["page"] = lastPageNumber.ToString();
        url = QueryHelpers.AddQueryString(baseUrl + endpoint, parameters!);
            
        response = await _requestService.GetApiResponse(url);
        if (response.IsFailed)
        {
            issues.Add(new Issue()
            {
                Name = "API response",
                Description = $"An error occurred when making a request to the `{endpoint}` endpoint",
                Message = response.Errors.First().Message,
                Parameters = parameters,
                Endpoint = url
            });
            return issues;
        }
        
        IPage? lastPage = schemaVersion == HSDSUKVersions.V3
            ? JsonConvert.DeserializeObject<PageV3>(response.Value.ToJsonString(), _serializerSettings)
            : JsonConvert.DeserializeObject<PageV1>(response.Value.ToJsonString());

        if (lastPage != null)
        {
            issues.AddRange(ValidatePageDetails(lastPage, parameters, endpoint, lastPageNumber, firstPage.TotalPages));
        }
        
        return Result.Ok(issues);
    }

    public IEnumerable<Issue> ValidatePageDetails(IPage currentPage, Dictionary<string, string> parameters, string endpoint, int page, int totalPages)
    {
        var issues = new List<Issue>();
        
        // Is the 'first_page' flag returned correctly
        (bool correct, Issue? issue) firstPageFlagTest = IsFirstPageFlagCorrect(currentPage.FirstPage, page, parameters, endpoint);
        if (!firstPageFlagTest.correct) 
            issues.Add(firstPageFlagTest.issue!);
        
        // Is the 'last_page' flag returned correctly
        (bool correct, Issue? issue) lastPageFlagTest = IstLastPageFlagCorrect(currentPage.LastPage, page, totalPages, parameters, endpoint);
        if (!lastPageFlagTest.correct) 
            issues.Add(lastPageFlagTest.issue!);
        
        // Is the 'empty' flag returned correctly
        (bool correct, Issue? issue) emptyFlagTest = IsEmptyFlagCorrect(currentPage.Empty, currentPage.Contents.Count, parameters, endpoint); 
        if (!emptyFlagTest.correct) 
            issues.Add(emptyFlagTest.issue!);
        
        // Is the number of items returned per page correct
        (bool correct, Issue? issue) itemsPerPageTest = IsItemsPerPageCorrect(currentPage.Contents.Count, currentPage.LastPage, PerPage, parameters, endpoint);
        if (!itemsPerPageTest.correct) 
            issues.Add(itemsPerPageTest.issue!);
        
        // Does the number of items returned match the 'size' value in the response
        (bool correct, Issue? issue) sizeTest = IsSizeCorrect(currentPage.Size, currentPage.Contents.Count, parameters, endpoint);
        if (!sizeTest.correct) 
            issues.Add(sizeTest.issue!);
        
        return issues;
    }

    public (bool, Issue?) IsFirstPageFlagCorrect(bool firstPageFlag, int page, Dictionary<string, string> parameters, string endpoint)
    {
        if (page == 1 && firstPageFlag)
            return (true, null);
        
        if (page != 1 && !firstPageFlag)
            return (true, null);
        
        return (false, new Issue
        {
            Name = "First page flag",
            Description = "Is the 'first_page' flag returned correctly",
            Message = $"The value of 'first_page' is {firstPageFlag} when the page number is {page}",
            Parameters = parameters,
            Endpoint = endpoint
        });
    }
    
    public (bool, Issue?) IstLastPageFlagCorrect(bool lastPageFlag, int page, int totalPages, Dictionary<string, string> parameters, string endpoint)
    {
        if (page == totalPages && lastPageFlag)
            return (true, null);
        
        if (page != totalPages && !lastPageFlag)
            return (true, null);
        
        return (false, new Issue
        {
            Name = "Last page flag",
            Description = "Is the 'last_page' flag returned correctly",
            Message = $"The value of 'last_page' is {lastPageFlag} when the page number is {page} of {totalPages}",
            Parameters = parameters,
            Endpoint = endpoint
        });
    }
    
    public (bool, Issue?) IsEmptyFlagCorrect(bool emptyFlag, int itemCount, Dictionary<string, string> parameters, string endpoint)
    {
        if (emptyFlag && itemCount == 0)
            return (true, null);
        
        if (!emptyFlag && itemCount > 0)
            return (true, null);
        
        return (false, new Issue
        {
            Name = "Empty flag",
            Description = "Is the 'empty' flag returned correctly",
            Message = $"The value of 'empty' is {emptyFlag} when {itemCount} were returned in the response",
            Parameters = parameters,
            Endpoint = endpoint
        });
    }
    
    public (bool, Issue?) IsItemsPerPageCorrect(int itemCount, bool lastPageFlag, int perPage, Dictionary<string, string> parameters, string endpoint)
    {
        if (itemCount == perPage)
            return (true, null);
        
        if (itemCount < perPage && lastPageFlag)
            return (true, null);
        
        return (false, new Issue
        {
            Name = "Items per page",
            Description = "Is the number of items returned per page correct",
            Message = $"The number of items returned is {itemCount} when {PerPage} item(s) were requested in the 'per_page' parameter",
            Parameters = parameters,
            Endpoint = endpoint
        });
    }
    
    public (bool, Issue?) IsSizeCorrect(int size, int itemCount, Dictionary<string, string> parameters, string endpoint)
    {
        if (size == itemCount)
            return (true, null);
        
        return (false, new Issue
        {
            Name = "Item count",
            Description = "Does the number of items returned match the 'size' value in the response",
            Message = $"The value of 'size' is {size} when {itemCount} item(s) were returned in the response content",
            Parameters = parameters,
            Endpoint = endpoint
        });
    }
}