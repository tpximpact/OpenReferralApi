using System.Text.Json;
using FluentResults;
using OpenReferralApi.Models;
using OpenReferralApi.Repositories.Interfaces;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class DashboardService : IDashboardService
{
    private readonly IDataRepository _dataRepository;

    public DashboardService(IDataRepository dataRepository)
    {
        _dataRepository = dataRepository;
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
    
}