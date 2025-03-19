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
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IDataRepository dataRepository, IValidatorService validatorService, 
        IGithubService githubService, ILogger<DashboardService> logger)
    {
        _dataRepository = dataRepository;
        _validatorService = validatorService;
        _githubService = githubService;
        _logger = logger;
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

    public async Task<Result> ValidateDashboardServices()
    {
        var services = await _dataRepository.GetServices();

        foreach (var service in services.Value)
        {
            _logger.LogInformation($"Periodic validation for {service.Name.Value} - Starting");
            try
            {
                if (service.ServiceUrl?.Url == null)
                {
                    var updateResult = await _dataRepository
                        .UpdateServiceTestStatus(service.Id!, Success.Fail, Success.Fail);
                    if (updateResult.IsFailed)
                        _logger.LogInformation($"Periodic validation for {service.Name.Value} - Dashboard status could not be updated");
                }

                var validationResult = await _validatorService.ValidateService(service.ServiceUrl!.Url!, "HSDS-UK-3.0");

                if (validationResult.Value.Service.IsValid)
                {
                    var updateResult = await _dataRepository
                        .UpdateServiceTestStatus(service.Id!, Success.Pass, Success.Pass);
                    if (updateResult.IsFailed)
                        _logger.LogInformation($"Periodic validation for {service.Name.Value} - Dashboard status could not be updated");
                }
                else
                {
                    var updateResult = await _dataRepository
                        .UpdateServiceTestStatus(service.Id!, Success.Fail, Success.Fail);
                    if (updateResult.IsFailed)
                        _logger.LogInformation($"Periodic validation for {service.Name.Value} - Dashboard status could not be updated");
                }
                    
                _logger.LogInformation($"Periodic validation for {service.Name.Value} - Completed");
            }
            catch (Exception e)
            {
                _logger.LogError($"Periodic validation for {service.Name.Value} - Failed with an error");
                _logger.LogError(e.Message);
            }
        }
        
        return Result.Ok();
    }
    
}