using FluentResults;
using OpenReferralApi.Models;
using OpenReferralApi.Repositories.Interfaces;
using OpenReferralApi.Services.Interfaces;
using Success = OpenReferralApi.Models.Success;

namespace OpenReferralApi.Services;

public class DashboardService : IDashboardService
{
    private readonly IDataRepository _dataRepository;
    private readonly IValidatorService _validatorService;
    private readonly IGithubService _githubService;
    private readonly IRequestService _requestService;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IDataRepository dataRepository, IValidatorService validatorService, 
        IGithubService githubService, IRequestService requestService, ILogger<DashboardService> logger)
    {
        _dataRepository = dataRepository;
        _validatorService = validatorService;
        _githubService = githubService;
        _logger = logger;
        _requestService = requestService;
    }

    public async Task<Result<DashboardOutput>> GetServices()
    {
        var response = new DashboardOutput
        {
            Definitions = new Definitions()
        };
        
        var services = await _dataRepository.GetServices();
        response.Data = services.Value.Select(serviceData => new Service(serviceData)).ToList();

        var columnData = await _dataRepository.GetColumns();
        response.Definitions.Columns = columnData.Value.ToDictionary(c => c.Name, c => c);

        var viewData = await _dataRepository.GetViews();
        response.Definitions.Views = viewData.Value.ToDictionary(v => v.Name, v => v);

        return response;
    }

    public async Task<Result<DashboardServiceDetails>> GetServiceById(string id)
    {
        var serviceDetails = await _dataRepository.GetServiceById(id);
        return new DashboardServiceDetails(serviceDetails.Value);
    }

    public async Task<Result<SubmissionResponse>> SubmitService(DashboardSubmission submission)
    {
        var newServiceData = new ServiceData(submission);
        var addServiceResult = await _dataRepository.AddService(newServiceData);
        if (addServiceResult.IsFailed)
            return Result.Fail("Failed to save submission details");

        return await _githubService.RaiseIssue(submission);
    }

    public async Task<Result<List<DashboardValidationResponse>>> ValidateDashboardServices()
    {
        var testingResult = new List<DashboardValidationResponse>();
        var services = await _dataRepository.GetServices();

        foreach (var service in services.Value)
        {
            _logger.LogInformation($"Dashboard validation for {service.Name!.Value} - Starting");
            var testResult = new DashboardValidationResponse()
            {
                Id = service.Id!,
                Name = service.Name.Value!.ToString()!,
                Version = service.SchemaVersion!.Value!.ToString()!
            };
            
            try
            {
                if (service.ServiceUrl?.Url == null)
                {
                    await _dataRepository.UpdateServiceTestStatus(service.Id!, Success.Fail, Success.Fail);
                    testResult.TestsPassed = false;
                    testResult.ServiceAvailable = false;
                    testResult.Message = $"Dashboard validation for {service.Name.Value} - No service url available";
                    testingResult.Add(testResult);
                    continue;
                }

                var validationResult = await _validatorService.ValidateService(service.ServiceUrl!.Url!, null);

                if (!validationResult.Value.Service.IsValid)
                {
                    testResult.TestsPassed = false;
                    var serviceAvailable = await IsServiceAvailable(service.ServiceUrl.Value!.ToString()!);
                    testResult.ServiceAvailable = serviceAvailable.IsSuccess;
                    testResult.Message = $"Dashboard validation for {service.Name.Value} - Tests failed";
                    testingResult.Add(testResult);
                    var apiStatus = serviceAvailable.IsSuccess
                        ? Success.Pass
                        : Success.Fail;
                    await _dataRepository.UpdateServiceTestStatus(service.Id!, apiStatus, Success.Fail);
                    continue;
                }
                
                await _dataRepository.UpdateServiceTestStatus(service.Id!, Success.Pass, Success.Pass);
                testResult.TestsPassed = true;
                testResult.ServiceAvailable = true;
                testingResult.Add(testResult);
            }
            catch (Exception e)
            {
                _logger.LogError($"Dashboard validation for {service.Name.Value} - Failed with an error");
                _logger.LogError(e.Message);
                testResult.TestsPassed = false;
                testResult.ServiceAvailable = false;
                testResult.Message = $"Dashboard validation for {service.Name.Value} - Critically failed with an error. Check the logs for more details";
                testingResult.Add(testResult);
            }
        }
        
        return Result.Ok(testingResult);
    }

    private async Task<Result> IsServiceAvailable(string serviceUrl)
    {
        serviceUrl = serviceUrl.TrimEnd('/');
        serviceUrl += "/services";

        var isUrlValid = Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uriResult) 
                         && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        
        if (!isUrlValid)
            return Result.Fail("Invalid URL provided");

        var response = await _requestService.GetApiResponse(serviceUrl);

        return response.IsSuccess
            ? Result.Ok() 
            : Result.Fail("Request failure");
    }
}