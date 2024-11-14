using FluentResults;
using OpenReferralApi.Models;

namespace OpenReferralApi.Repositories.Interfaces;

public interface IDataRepository
{
    public Task<Result<List<ServiceData>>> GetServices();
    public Task<Result<ServiceData>> GetServiceById(string id);
    public Task<Result<List<Field>>> GetColumns();
    public Task<Result<List<View>>> GetViews();

}