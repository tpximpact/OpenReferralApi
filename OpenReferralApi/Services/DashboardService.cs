using FluentResults;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Requests;
using OpenReferralApi.Models.Responses;
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

    public async Task<Result<DashboardResponse>> GetServices()
    {
        var response = new DashboardResponse
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

    public async Task<Result<ServiceDetailsResponse>> GetServiceById(string id)
    {
        var serviceDetails = await _dataRepository.GetServiceById(id);
        return new ServiceDetailsResponse(serviceDetails.Value);
    }


    public async Task<Result<SubmissionResponse>> SubmitService(DashboardSubmissionRequest submission)
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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {

            var validationResponses = await Task.WhenAll(services.Value.Select(service => ValidateOneService(service)).ToArray());
            foreach (var validationResponse in validationResponses)
            {
                await _dataRepository.UpdateServiceTestStatus(validationResponse.Id, validationResponse.ServiceAvailable ? Success.Pass : Success.Fail, validationResponse.TestsPassed ? Success.Pass : Success.Fail);
                testingResult.Add(validationResponse);
            }

            return Result.Ok(testingResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while validating dashboard services");
            return Result.Fail("An error occurred while validating the dashboard services");
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation($"Dashboard validation completed in {stopwatch.ElapsedMilliseconds} ms");
        }
    }

    public async Task<Result<List<DashboardValidationResponse>>> ValidateDashboardService(string id)
    {
        var service = await _dataRepository.GetServiceById(id);
        if (service.IsFailed)
            return Result.Fail("Service not found");

        var validationResult = await ValidateOneService(service.Value);
        return Result.Ok(new List<DashboardValidationResponse> { validationResult });
    }

    private async Task<DashboardValidationResponse> ValidateOneService(ServiceData service)
    {
        _logger.LogInformation($"Dashboard validation for {service.Name!.Value} - Starting");

        DashboardValidationResponse testResult = new DashboardValidationResponse()
        {
            Id = service.Id!,
            Name = service.Name.Value!.ToString()!,
            Version = service.SchemaVersion!.Value!.ToString()!,
            Service = service.ServiceUrl!.Url!
        };

        try
        {
            if (testResult.Version == "1.0")
            {
                testResult.Service = service.ServiceUrl?.Value?.ToString() ?? string.Empty;
            }

            var serviceAvailable = await IsServiceAvailable(testResult.Service);
            if (serviceAvailable.IsFailed)
            {
                testResult.TestsPassed = false;
                testResult.ServiceAvailable = false;
                testResult.Message = "Service unavailable";
            }
            else
            {
                testResult.ServiceAvailable = true;

                var validationResult = await _validatorService.ValidateService(testResult.Service, null);

                if (!validationResult.Value.Service.IsValid)
                {
                    testResult.TestsPassed = false;
                    testResult.Message = "Tests failed";
                    testResult.Results = validationResult.Value.TestSuites.SelectMany(ts => ts.Tests).ToList();
                }
                else
                {
                    testResult.TestsPassed = true;
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation for {service} - Failed with an error", service.Name.Value);

            testResult.TestsPassed = false;
            testResult.ServiceAvailable = false;
            testResult.Message = "Critically failed with an error. Check the logs for more details";
        }

        return testResult;
    }

    private async Task<Result> IsServiceAvailable(string? serviceUrl)
    {
        if (string.IsNullOrEmpty(serviceUrl))
            return Result.Fail("Invalid URL provided");

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