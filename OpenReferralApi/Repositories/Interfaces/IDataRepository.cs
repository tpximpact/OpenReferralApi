using FluentResults;
using OpenReferralApi.Models;
using Success = OpenReferralApi.Models.Success;

namespace OpenReferralApi.Repositories.Interfaces;

public interface IDataRepository
{
    public Task<Result<List<ServiceData>>> GetServices();
    public Task<Result<ServiceData>> GetServiceById(string id);
    public Task<Result<List<Field>>> GetColumns();
    public Task<Result<List<View>>> GetViews();
    public Task<Result> UpdateServiceTestStatus(string id, Success apiStatus, Success testStatus);
    public Task<Result<string?>> AddService(ServiceData newService);

}