using FluentResults;
using OpenReferralApi.Models;

namespace OpenReferralApi.Services.Interfaces;

public interface IPaginationTestingService
{
    public Task<Result<List<Issue>>> ValidatePagination(string baseUrl, string endpoint);
    public IEnumerable<Issue> ValidatePageDetails(Page currentPage, Dictionary<string, string> parameters, string endpoint, int page, int totalPages);
    public (bool, Issue?) IsFirstPageFlagCorrect(bool firstPageFlag, int page, Dictionary<string, string> parameters, string endpoint);
    public (bool, Issue?) IstLastPageFlagCorrect(bool lastPageFlag, int page, int totalPages, Dictionary<string, string> parameters, string endpoint);
    public (bool, Issue?) IsEmptyFlagCorrect(bool emptyFlag, int itemCount, Dictionary<string, string> parameters, string endpoint);
    public (bool, Issue?) IsItemsPerPageCorrect(int itemCount, bool lastPageFlag, int perPage, Dictionary<string, string> parameters, string endpoint);
    public (bool, Issue?) IsSizeCorrect(int size, int itemCount, Dictionary<string, string> parameters, string endpoint);
}