using FluentResults;
using OpenReferralApi.Models;

namespace OpenReferralApi.Repositories.Interfaces;

public interface IDataRepository
{
    public Task<Result<List<Service>>> GetServices();
    public Task<Result<List<Field>>> GetColumns();
    public Task<Result<List<View>>> GetViews();

}